using System.Globalization;
using Microsoft.Playwright;

namespace Flox.Sqlite.BrowserTests;

// Several tabs of one pool under load (architecture.md, the data of the browser clients in SQLite over OPFS): the leading tab runs the worker, the
// others ask it over a BroadcastChannel. Nothing written may be lost, a count never goes back, the lead moves when its tab closes, a closed tab does
// not hold its bases. The times are written to the output of the test; the bounds are loose, they catch a hang, not a slow machine
[TestFixture]
[Explicit("Needs Chrome or Edge on the machine (FLOX_BROWSER_CHANNEL)")]
[Category("Browser")]
public sealed class SqliteTabsLoadTests
{
    private const string Base = "load.sqlite";
    private const string Schema = "CREATE TABLE IF NOT EXISTS rows (tab INTEGER NOT NULL, n INTEGER NOT NULL, payload TEXT NOT NULL, PRIMARY KEY (tab, n))";

    private IBrowserContext _context = null!;
    private string _pool = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _context = await BrowserHarness.Browser.NewContextAsync();
        _pool = "load-" + Guid.NewGuid().ToString("N")[..8];
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        await _context.DisposeAsync();
    }

    [Test]
    public async Task FourTabs_WriteAtOnce_EveryRowArrives()
    {
        var tabs = await OpenTabsAsync(4);
        var handles = await OpenBaseAsync(tabs);

        var writes = tabs.Select((tab, i) => WriteAsync(tab, handles[i], i)).ToArray();
        var times = await Task.WhenAll(writes);

        foreach (var (tab, i) in tabs.Select((tab, i) => (tab, i)))
        {
            Assert.That(await CountAsync(tab, handles[i]), Is.EqualTo(4 * (300 + 2000)), $"tab {i} sees every row");
        }

        await Report("one statement, leader", times[0].OneByOne);
        await Report("one statement, the other tabs", [.. times.Skip(1).SelectMany(time => time.OneByOne)]);
        await Report("a batch of 100, leader", times[0].Batches);
        await Report("a batch of 100, the other tabs", [.. times.Skip(1).SelectMany(time => time.Batches)]);
        Assert.That(Percentile([.. times.Skip(1).SelectMany(time => time.OneByOne)], 0.95), Is.LessThan(500), "a statement of another tab, 95th percentile (ms)");
    }

    [Test]
    public async Task TheLeaderCloses_UnderLoad_TheOthersFinishTheirWrites()
    {
        var tabs = await OpenTabsAsync(3);
        var handles = await OpenBaseAsync(tabs);

        var first = tabs[1].EvaluateAsync<double[]>("([h, t, c]) => window.harness.writeOneByOne(h, t, c)", new object[] { handles[1], 1, 400 });
        var second = tabs[2].EvaluateAsync<double[]>("([h, t, c]) => window.harness.writeOneByOne(h, t, c)", new object[] { handles[2], 2, 400 });
        await Task.Delay(500);
        await tabs[0].CloseAsync();
        var times = await Task.WhenAll(first, second);

        Assert.That(await CountAsync(tabs[1], handles[1]), Is.EqualTo(800), "every row of both tabs, none twice");
        Assert.That(await CountAsync(tabs[2], handles[2]), Is.EqualTo(800));
        var slowest = times.SelectMany(time => time).Max();
        await TestContext.Out.WriteLineAsync($"the longest statement while the lead moved: {slowest:F0} ms");
        Assert.That(slowest, Is.LessThan(10_000), "the lead moved within the attempts of the new worker");
    }

    [Test]
    public async Task ATabReadsWhileAnotherWrites_TheCountOnlyGrows()
    {
        var tabs = await OpenTabsAsync(3);
        var handles = await OpenBaseAsync(tabs);

        var reading = tabs[2].EvaluateAsync<long[]>("([h]) => window.harness.countWhile(h, 'writerDone')", new object[] { handles[2] });
        await tabs[1].EvaluateAsync<double[]>("([h, t, c, s]) => window.harness.writeBatches(h, t, c, s)", new object[] { handles[1], 1, 2000, 50 });
        await tabs[2].EvaluateAsync("() => window.writerDone = true");
        var counts = await reading;

        Assert.That(counts, Is.Not.Empty);
        Assert.That(counts, Is.Ordered, "a count never goes back: a batch is seen whole or not at all");
        Assert.That(counts.All(count => count % 50 == 0), Is.True, "never a part of a batch");
        Assert.That(await CountAsync(tabs[2], handles[2]), Is.EqualTo(2000));
        await TestContext.Out.WriteLineAsync($"{counts.Length} counts read while 40 batches were written");
    }

    [Test]
    public async Task AnotherTab_ReadsTwentyThousandRows_InOneAnswer()
    {
        var tabs = await OpenTabsAsync(2);
        var handles = await OpenBaseAsync(tabs);
        await tabs[0].EvaluateAsync<double[]>("([h, t, c, s]) => window.harness.writeBatches(h, t, c, s)", new object[] { handles[0], 0, 20_000, 1000 });

        var leader = await TimeAsync(() => tabs[0].EvaluateAsync<int>("async ([h]) => (await window.harness.query(h, 'SELECT * FROM rows')).length", new object[] { handles[0] }));
        var other = await TimeAsync(() => tabs[1].EvaluateAsync<int>("async ([h]) => (await window.harness.query(h, 'SELECT * FROM rows')).length", new object[] { handles[1] }));

        Assert.That(leader.Value, Is.EqualTo(20_000));
        Assert.That(other.Value, Is.EqualTo(20_000));
        await TestContext.Out.WriteLineAsync($"20000 rows of 200 characters: leader {leader.Milliseconds:F0} ms, another tab {other.Milliseconds:F0} ms");
        Assert.That(other.Milliseconds, Is.LessThan(10_000));
    }

    [Test]
    public async Task AnotherTab_PaysLittleForTheChannel()
    {
        var tabs = await OpenTabsAsync(2);
        var handles = await OpenBaseAsync(tabs);
        const string Reads = "async ([h, c]) => { const times = []; for (let i = 0; i < c; i++) { const s = performance.now(); " +
                             "await window.harness.query(h, 'SELECT count(*) FROM rows WHERE tab = $t', { $t: i }); times.push(performance.now() - s); } return times; }";
        const string Writes = "([h, t, c]) => window.harness.writeOneByOne(h, t, c)";

        var leaderReads = await tabs[0].EvaluateAsync<double[]>(Reads, new object[] { handles[0], 500 });
        var otherReads = await tabs[1].EvaluateAsync<double[]>(Reads, new object[] { handles[1], 500 });
        var leaderWrites = await tabs[0].EvaluateAsync<double[]>(Writes, new object[] { handles[0], 0, 100 });
        var otherWrites = await tabs[1].EvaluateAsync<double[]>(Writes, new object[] { handles[1], 1, 100 });

        await Report("a small read, leader", leaderReads);
        await Report("a small read, another tab", otherReads);
        await Report("one statement alone, leader", leaderWrites);
        await Report("one statement alone, another tab", otherWrites);
        Assert.That(Percentile(otherReads, 0.5), Is.LessThan(Percentile(leaderReads, 0.5) + 20), "the channel adds little to a read (ms, median)");
    }

    [Test]
    public async Task TheLeaderIsPausedInADebugger_AnotherTabFailsInsteadOfHanging()
    {
        var tabs = await OpenTabsAsync(2);
        var handles = await OpenBaseAsync(tabs);
        var debugger = await _context.NewCDPSessionAsync(tabs[0]);
        await debugger.SendAsync("Debugger.enable");
        // Paused at the next script it runs: the request of the other tab
        await debugger.SendAsync("Debugger.pause");

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var failed = await Assert.ThrowsAsync<PlaywrightException>(() => CountAsync(tabs[1], handles[1]));
        var waited = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds;
        await debugger.SendAsync("Debugger.resume");

        Assert.That(failed!.Message, Does.Contain("[5]").And.Contain("does not answer"));
        Assert.That(waited, Is.InRange(9, 20), "the time a tab waits for the confirmation (s)");
        Assert.That(await CountAsync(tabs[1], handles[1]), Is.Zero, "the leader answers again once it runs");
    }

    [Test]
    public async Task ATabClosedWithItsBaseOpen_DoesNotStopTheDelete()
    {
        var tabs = await OpenTabsAsync(2);
        var handle = await tabs[1].EvaluateAsync<int>("() => window.harness.open('gone.sqlite')");
        await tabs[1].EvaluateAsync<int>("([h, s]) => window.harness.execute(h, s)", new object[] { handle, Schema });

        var refused = await Assert.ThrowsAsync<PlaywrightException>(() => tabs[0].EvaluateAsync("() => window.harness.remove('gone.sqlite')"));
        Assert.That(refused!.Message, Does.Contain("[5]"), "open in a living tab: refused");

        await tabs[1].CloseAsync();
        await tabs[0].EvaluateAsync("() => window.harness.remove('gone.sqlite')");
        var names = await tabs[0].EvaluateAsync<string[]>("() => window.harness.list()");
        Assert.That(names, Does.Not.Contain("gone.sqlite"));
    }

    [Test]
    public async Task ABaseBeingImported_IsThereForNobody_UntilItIsComplete()
    {
        const string Name = "held.sqlite";
        var tabs = await OpenTabsAsync(2);
        var address = new Uri(BrowserHarness.Address, "held.sqlite.gz").AbsoluteUri;
        var started = BrowserHarness.HoldDownload();
        var importing = tabs[0].EvaluateAsync<long>("([n, u]) => window.harness.importGzip(n, u)", new object[] { Name, address });
        await started;
        // The first half reaches the worker
        await Task.Delay(300);

        Assert.That(await tabs[1].EvaluateAsync<bool>("n => window.harness.exists(n)", Name), Is.False);
        Assert.That(await tabs[1].EvaluateAsync<string[]>("() => window.harness.list()"), Does.Not.Contain(Name));
        foreach (var call in new[] { "n => window.harness.remove(n)", "n => window.harness.open(n)", $"n => window.harness.importGzip(n, '{address}')" })
        {
            var refused = await Assert.ThrowsAsync<PlaywrightException>(() => tabs[1].EvaluateAsync(call, Name));
            Assert.That(refused!.Message, Does.Contain("[5]"), call);
        }

        BrowserHarness.ReleaseDownload();
        Assert.That(await importing, Is.EqualTo(512));
        Assert.That(await tabs[1].EvaluateAsync<bool>("n => window.harness.exists(n)", Name), Is.True);
        var handle = await tabs[1].EvaluateAsync<int>("n => window.harness.open(n, true)", Name);
        var version = await tabs[1].EvaluateAsync<int>("async h => (await window.harness.query(h, 'PRAGMA user_version'))[0][0]", handle);
        Assert.That(version, Is.EqualTo(BrowserHarness.HeldUserVersion), "the base came whole");
    }

    [Test]
    public async Task AnImportCancelled_LeavesNothing_AndTheNameIsFreeAgain()
    {
        const string Name = "held.sqlite";
        var tabs = await OpenTabsAsync(2);
        var address = new Uri(BrowserHarness.Address, "held.sqlite.gz").AbsoluteUri;
        var started = BrowserHarness.HoldDownload();
        var importing = tabs[1].EvaluateAsync<long>("([n, u]) => window.harness.importGzip(n, u)", new object[] { Name, address });
        await started;
        await Task.Delay(300);

        await tabs[1].EvaluateAsync("n => window.harness.cancelImport(n)", Name);
        var cancelled = await Assert.ThrowsAsync<PlaywrightException>(() => importing);
        Assert.That(cancelled!.Message, Does.Contain("[9]"));
        Assert.That(await tabs[0].EvaluateAsync<bool>("n => window.harness.exists(n)", Name), Is.False);

        started = BrowserHarness.HoldDownload();
        importing = tabs[1].EvaluateAsync<long>("([n, u]) => window.harness.importGzip(n, u)", new object[] { Name, address });
        await started;
        BrowserHarness.ReleaseDownload();
        Assert.That(await importing, Is.EqualTo(512));
        Assert.That(await tabs[0].EvaluateAsync<bool>("n => window.harness.exists(n)", Name), Is.True);
    }

    [Test]
    public async Task ThePool_GrowsWithItsBases()
    {
        var tabs = await OpenTabsAsync(1);
        const string Fill = "async ([c, s]) => { for (let i = 0; i < c; i++) { const h = await window.harness.open(`many-${i}.sqlite`); " +
                            "await window.harness.execute(h, s); await window.harness.writeOneByOne(h, i, 1); } return (await window.harness.list()).length; }";

        var bases = await tabs[0].EvaluateAsync<int>(Fill, new object[] { 24, Schema });

        Assert.That(bases, Is.EqualTo(24), "more bases than the 16 files the pool started with, each written with its journal");
    }

    [Test]
    public async Task APage_HoldsOnePool()
    {
        var tabs = await OpenTabsAsync(1);

        var refused = await Assert.ThrowsAsync<PlaywrightException>(() => tabs[0].EvaluateAsync("() => window.harness.init('another-pool')"));
        Assert.That(refused!.Message, Does.Contain(_pool).And.Contain("another-pool"));
        Assert.That(await tabs[0].EvaluateAsync<string>("p => window.harness.init(p)", _pool), Is.Not.Empty, "the same pool again is the same pool");
    }

    [Test]
    public async Task AHiddenLeader_GivesTheLeadToTheTabThatAsks_AndNothingIsLost()
    {
        var tabs = await OpenTabsAsync(2);
        var handles = await OpenBaseAsync(tabs);
        await tabs[0].EvaluateAsync<double[]>("([h, t, c]) => window.harness.writeOneByOne(h, t, c)", new object[] { handles[0], 0, 50 });
        Assert.That(await LeadsAsync(tabs[0]), Is.True);

        // Hidden without the event of the browser (a phone may not send it in time): the request of the tab the user looks at moves the lead
        await tabs[0].EvaluateAsync("() => window.harness.hide(true, false)");
        await tabs[1].EvaluateAsync<double[]>("([h, t, c]) => window.harness.writeOneByOne(h, t, c)", new object[] { handles[1], 1, 50 });
        await tabs[1].WaitForFunctionAsync("() => window.harness.leads()");
        Assert.That(await LeadsAsync(tabs[0]), Is.False);
        Assert.That(await CountAsync(tabs[0], handles[0]), Is.EqualTo(100), "the tab that led reads through the new leader");

        // Back: the first tab is shown, the second hidden with the event
        await tabs[0].EvaluateAsync("() => window.harness.hide(false, true)");
        await tabs[1].EvaluateAsync("() => window.harness.hide(true, true)");
        await tabs[0].WaitForFunctionAsync("() => window.harness.leads()");
        await tabs[0].EvaluateAsync<double[]>("([h, t, c]) => window.harness.writeOneByOne(h, t, c)", new object[] { handles[0], 2, 50 });
        Assert.That(await CountAsync(tabs[1], handles[1]), Is.EqualTo(150));
    }

    private static Task<bool> LeadsAsync(IPage tab)
    {
        return tab.EvaluateAsync<bool>("() => window.harness.leads()");
    }

    private async Task<IPage[]> OpenTabsAsync(int count)
    {
        // One after another: the first one leads
        var tabs = new List<IPage>();
        for (var i = 0; i < count; i++)
        {
            tabs.Add(await BrowserHarness.OpenTabAsync(_context, _pool));
        }

        return [.. tabs];
    }

    private static async Task<int[]> OpenBaseAsync(IPage[] tabs)
    {
        var handles = new int[tabs.Length];
        for (var i = 0; i < tabs.Length; i++)
        {
            handles[i] = await tabs[i].EvaluateAsync<int>("name => window.harness.open(name)", Base);
        }

        await tabs[0].EvaluateAsync<int>("([h, s]) => window.harness.execute(h, s)", new object[] { handles[0], Schema });
        return handles;
    }

    private static async Task<WriteTimes> WriteAsync(IPage tab, int handle, int index)
    {
        var oneByOne = await tab.EvaluateAsync<double[]>("([h, t, c]) => window.harness.writeOneByOne(h, t, c)", new object[] { handle, index, 300 });
        var batches = await tab.EvaluateAsync<double[]>("([h, t, c, s]) => window.harness.writeBatches(h, t, c, s)", new object[] { handle, 100 + index, 2000, 100 });
        return new WriteTimes(oneByOne, batches);
    }

    private static async Task<long> CountAsync(IPage tab, int handle)
    {
        return await tab.EvaluateAsync<long>("async ([h]) => (await window.harness.query(h, 'SELECT count(*) FROM rows'))[0][0]", new object[] { handle });
    }

    private static async Task<Timed<T>> TimeAsync<T>(Func<Task<T>> action)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var value = await action();
        return new Timed<T>(value, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private static async Task Report(string what, double[] times)
    {
        await TestContext.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
            $"{what}: {times.Length} calls, median {Percentile(times, 0.5):F1} ms, 95th {Percentile(times, 0.95):F1} ms, max {times.Max():F1} ms"));
    }

    private static double Percentile(double[] times, double share)
    {
        var sorted = times.Order().ToArray();
        return sorted[Math.Min(sorted.Length - 1, (int)(share * sorted.Length))];
    }

    private sealed record WriteTimes(double[] OneByOne, double[] Batches);

    private sealed record Timed<T>(T Value, double Milliseconds);
}
