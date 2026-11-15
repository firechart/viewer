using System.Diagnostics;
using Flox.Core.Data;
using Flox.Sqlite;
using Flox.Sqlite.Files;

namespace Flox.Tests.Platform;

// What every engine of SQLite does the same (ISqliteEngine); the browser's is held to the same scenarios in a browser
public abstract class SqliteEngineContract
{
    protected ISqliteEngine Engine { get; private set; } = null!;

    protected abstract ISqliteEngine CreateEngine();

    protected virtual void Cleanup()
    {
    }

    [SetUp]
    public void SetUpEngine()
    {
        Engine = CreateEngine();
    }

    [TearDown]
    public void TearDownEngine()
    {
        Cleanup();
    }

    private static Dictionary<string, object?> P(params (string Name, object? Value)[] values)
    {
        return values.ToDictionary(row => row.Name, row => row.Value);
    }

    [Test]
    public async Task TheValuesOfEveryKind_GoInAndComeBackAsTheyWere()
    {
        await using var db = await Engine.OpenAsync("kinds.sqlite", SqliteAccess.ReadWrite);
        await db.ExecuteAsync("CREATE TABLE t (n INTEGER, r REAL, s TEXT, b BLOB, z TEXT)");

        var changed = await db.ExecuteAsync("INSERT INTO t VALUES ($n, $r, $s, $b, $z)",
                                            P(("$n", 9_007_199_254_740L), ("$r", 2.5), ("$s", "Пациент ✓"), ("$b", new byte[] { 0, 1, 255 }), ("$z", null)));
        var rows = await db.QueryAsync("SELECT n, r, s, b, z FROM t");

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(rows.Columns, Is.EqualTo(new[] { "n", "r", "s", "b", "z" }));
            Assert.That(rows[0][0], Is.TypeOf<long>());
            Assert.That(rows[0][1], Is.TypeOf<double>());
            Assert.That(rows[0].GetInt64(0), Is.EqualTo(9_007_199_254_740L));
            Assert.That(rows[0].GetDouble(1), Is.EqualTo(2.5));
            Assert.That(rows[0].GetString(2), Is.EqualTo("Пациент ✓"));
            Assert.That(rows[0].GetBytes(3), Is.EqualTo(new byte[] { 0, 1, 255 }));
            Assert.That(rows[0].IsNull(4), Is.True);
        });
    }

    [Test]
    public async Task ABatch_IsAllOrNothing()
    {
        await using var db = await Engine.OpenAsync("batch.sqlite", SqliteAccess.ReadWrite);
        await db.ExecuteAsync("CREATE TABLE t (k TEXT PRIMARY KEY)");

        var done = await db.ExecuteBatchAsync([new SqliteStatement("INSERT INTO t VALUES ('a')"), new SqliteStatement("INSERT INTO t VALUES ('b')")]);
        await Assert.ThrowsAsync<SqliteEngineException>(async () => await db.ExecuteBatchAsync([
            new SqliteStatement("INSERT INTO t VALUES ('c')"),
            new SqliteStatement("INSERT INTO t VALUES ('a')")
        ]));
        var keys = await db.QueryAsync("SELECT k FROM t ORDER BY k");

        Assert.That(done, Is.EqualTo(new[] { 1, 1 }));
        Assert.That(keys.Rows.Select(row => row.GetString(0)), Is.EqualTo(new[] { "a", "b" }), "the failed batch left nothing");
    }

    [Test]
    public async Task ABaseReadOnly_MustExist_AndRefusesWrites()
    {
        await Assert.ThrowsAsync<SqliteEngineException>(async () => await Engine.OpenAsync("missing.sqlite", SqliteAccess.ReadOnly));

        await using (var db = await Engine.OpenAsync("ro.sqlite", SqliteAccess.ReadWrite))
        {
            await db.ExecuteAsync("CREATE TABLE t (k TEXT)");
        }

        await using var read = await Engine.OpenAsync("ro.sqlite", SqliteAccess.ReadOnly);
        await Assert.ThrowsAsync<SqliteEngineException>(async () => await read.ExecuteAsync("INSERT INTO t VALUES ('x')"));
        Assert.That((await read.QueryAsync("SELECT count(*) FROM t")).Scalar(), Is.EqualTo(0L));
    }

    [Test]
    public async Task ABase_KeepsItsData_IsDeletedOnlyWhenClosed()
    {
        var db = await Engine.OpenAsync("keep.sqlite", SqliteAccess.ReadWrite);
        await db.ExecuteAsync("CREATE TABLE t (k TEXT)");
        await db.ExecuteAsync("INSERT INTO t VALUES ('x')");

        await Assert.ThrowsAsync<SqliteEngineException>(async () => await Engine.DeleteAsync("keep.sqlite"));
        await db.DisposeAsync();
        await using (var again = await Engine.OpenAsync("keep.sqlite", SqliteAccess.ReadWrite))
        {
            Assert.That((await again.QueryAsync("SELECT k FROM t")).Scalar(), Is.EqualTo("x"));
        }

        await Engine.DeleteAsync("keep.sqlite");
        Assert.That(await Engine.ExistsAsync("keep.sqlite"), Is.False);
    }

    [Test]
    public async Task CallsAtOnce_AreDoneOneByOne_WithoutLoss()
    {
        await using var db = await Engine.OpenAsync("parallel.sqlite", SqliteAccess.ReadWrite);
        await db.ExecuteAsync("CREATE TABLE t (n INTEGER)");

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => i % 4 == 0
            ? (Task)db.QueryAsync("SELECT count(*) FROM t")
            : db.ExecuteAsync("INSERT INTO t VALUES ($n)", P(("$n", (long)i)))));

        Assert.That((await db.QueryAsync("SELECT count(*) FROM t")).Scalar(), Is.EqualTo(150L));
    }

    [Test]
    public async Task TheDocuments_AreKeptByCollectionAndKey_AndSurviveAReopen()
    {
        await using (var store = new SqliteDocumentStore(Engine, "flox.sqlite"))
        {
            Assert.That(await store.LoadAsync("bookmarks"), Is.Empty);
            await store.PutAsync("bookmarks", [new LocalDocument("a", "{\"n\":1}"), new LocalDocument("b", "{\"n\":2}")]);
            await store.PutAsync("settings", [new LocalDocument("a", "{\"theme\":\"dark\"}")]);
            await store.PutAsync("bookmarks", [new LocalDocument("a", "{\"n\":3}")]);
            await store.DeleteAsync("bookmarks", ["b", "missing"]);
        }

        await using var reopened = new SqliteDocumentStore(Engine, "flox.sqlite");
        var bookmarks = await reopened.LoadAsync("bookmarks");
        var settings = await reopened.LoadAsync("settings");

        Assert.That(bookmarks, Is.EqualTo(new[] { new LocalDocument("a", "{\"n\":3}") }));
        Assert.That(settings, Is.EqualTo(new[] { new LocalDocument("a", "{\"theme\":\"dark\"}") }));
    }

    // The load of a client: thousands of bookmarks written at once and read back, then many small writes at once
    [Test]
    public async Task TenThousandDocuments_AreWrittenAndReadBack()
    {
        await using var store = new SqliteDocumentStore(Engine, "load.sqlite");
        var documents = Enumerable.Range(0, 10_000)
                                  .Select(i => new LocalDocument($"k{i}", $"{{\"title\":\"Bookmark {i}\",\"note\":\"{new string('x', 400)}\"}}"))
                                  .ToList();

        var clock = Stopwatch.StartNew();
        await store.PutAsync("bookmarks", documents);
        var written = clock.Elapsed;
        clock.Restart();
        var read = await store.LoadAsync("bookmarks");
        var loaded = clock.Elapsed;
        clock.Restart();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(i => store.PutAsync("settings", [new LocalDocument($"s{i}", "{}")]).AsTask()));
        var small = clock.Elapsed;

        await TestContext.Out.WriteLineAsync($"{GetType().Name}: put 10000 in {written.TotalMilliseconds:0} ms, load in {loaded.TotalMilliseconds:0} ms, 100 small puts in {small.TotalMilliseconds:0} ms");
        Assert.That(read, Has.Count.EqualTo(10_000));
        Assert.That(await store.LoadAsync("settings"), Has.Count.EqualTo(100));
    }
}

[TestFixture]
public class FileSqliteEngineTests : SqliteEngineContract
{
    private string _folder = null!;

    protected override ISqliteEngine CreateEngine()
    {
        _folder = Path.Combine(Path.GetTempPath(), "flox-sqlite-" + Guid.NewGuid().ToString("N"));
        return new FileSqliteEngine(_folder);
    }

    protected override void Cleanup()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    [Test]
    public async Task AName_IsNotAPath()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await Engine.OpenAsync("../escape.sqlite", SqliteAccess.ReadWrite));
        await Assert.ThrowsAsync<ArgumentException>(async () => await Engine.OpenAsync("sub/x.sqlite", SqliteAccess.ReadWrite));
    }
}
