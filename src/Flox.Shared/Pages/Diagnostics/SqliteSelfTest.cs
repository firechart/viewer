using System.Diagnostics;
using Flox.Core.Data;
using Flox.Sqlite;

namespace Flox.Shared.Pages.Diagnostics;

/// <summary>
/// The outcome of one scenario of <see cref="SqliteSelfTest"/>.
/// </summary>
/// <param name="Name">What the scenario checks.</param>
/// <param name="Passed">Whether it passed.</param>
/// <param name="Milliseconds">How long it took.</param>
/// <param name="Message">What it found, or why it failed.</param>
public sealed record SqliteCheck(string Name, bool Passed, double Milliseconds, string Message);

/// <summary>The scenarios of the contract of the engines (<c>SqliteEngineContract</c> of Flox.Tests) run on the engine of the host, in its browser: the page <c>/diagnostics/sqlite</c> shows them and a test of the browser reads them. Its bases are named <c>selftest-*.sqlite</c> and deleted at the end.</summary>
public sealed class SqliteSelfTest(ISqliteEngine engine, ILocalDocumentStore? compareWith = null)
{
    private const string Prefix = "selftest-";

    /// <summary>Runs every scenario and returns the outcome of each; a failure of one does not stop the others.</summary>
    public async Task<IReadOnlyList<SqliteCheck>> RunAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<SqliteCheck>();
        await RunAsync(checks, "values of every kind", KindsAsync, cancellationToken);
        await RunAsync(checks, "a batch is all or nothing", BatchAsync, cancellationToken);
        await RunAsync(checks, "read only must exist and refuses writes", ReadOnlyAsync, cancellationToken);
        await RunAsync(checks, "the data survives a reopen, deleted only when closed", KeepAsync, cancellationToken);
        await RunAsync(checks, "200 calls at once", ParallelAsync, cancellationToken);
        await RunAsync(checks, "documents by collection and key", DocumentsAsync, cancellationToken);
        await RunAsync(checks, "10000 documents written and read back", LoadAsync, cancellationToken);
        if (compareWith is not null && compareWith is not SqliteDocumentStore)
        {
            await RunAsync(checks, $"the same 10000 in {compareWith.GetType().Name}, for comparison", CompareAsync, cancellationToken);
        }
        return checks;
    }

    private static async Task RunAsync(List<SqliteCheck> checks, string name, Func<CancellationToken, Task<string>> check, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var message = await check(cancellationToken);
            checks.Add(new SqliteCheck(name, true, clock.Elapsed.TotalMilliseconds, message));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var where = exception.StackTrace?.Split(Environment.NewLine).FirstOrDefault()?.Trim();
            checks.Add(new SqliteCheck(name, false, clock.Elapsed.TotalMilliseconds, exception.GetType().Name + ": " + exception.Message + " " + where));
        }
    }

    private async Task<ISqliteDatabase> FreshAsync(string name, CancellationToken cancellationToken)
    {
        if (await engine.ExistsAsync(Prefix + name, cancellationToken))
        {
            await engine.DeleteAsync(Prefix + name, cancellationToken);
        }

        return await engine.OpenAsync(Prefix + name, SqliteAccess.ReadWrite, cancellationToken);
    }

    private async Task DropAsync(ISqliteDatabase database, CancellationToken cancellationToken)
    {
        await database.DisposeAsync();
        await engine.DeleteAsync(database.Name, cancellationToken);
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException(what);
        }
    }

    private static async Task ExpectFailureAsync(Func<Task> call, string what)
    {
        try
        {
            await call();
        }
        catch (SqliteEngineException)
        {
            return;
        }

        throw new InvalidOperationException(what);
    }

    private async Task<string> KindsAsync(CancellationToken cancellationToken)
    {
        var db = await FreshAsync("kinds.sqlite", cancellationToken);
        await db.ExecuteAsync("CREATE TABLE t (n INTEGER, big INTEGER, r REAL, s TEXT, b BLOB, z TEXT)", null, cancellationToken);
        var blob = Enumerable.Range(0, 70_000).Select(i => (byte)i).ToArray();
        await db.ExecuteAsync("INSERT INTO t VALUES ($n, $big, $r, $s, $b, $z)", new Dictionary<string, object?>
        {
            ["$n"] = 9_007_199_254_740L, ["$big"] = 9_007_199_254_740_993L, ["$r"] = 2.5, ["$s"] = "Пациент ✓", ["$b"] = blob, ["$z"] = null
        }, cancellationToken);
        var row = (await db.QueryAsync("SELECT n, big, r, s, b, z FROM t", null, cancellationToken))[0];
        Expect(row[0] is long && row[1] is long && row[2] is double && row[3] is string && row[4] is byte[], "the types of the values");
        Expect(row.GetInt64(0) == 9_007_199_254_740L, "the integer");
        Expect(row.GetInt64(1) == 9_007_199_254_740_993L, "the integer beyond 2^53");
        Expect(row.GetDouble(2) == 2.5, "the real");
        Expect(row.GetString(3) == "Пациент ✓", "the text");
        Expect(row.GetBytes(4).SequenceEqual(blob), "the blob of 70 KB");
        Expect(row.IsNull(5), "the null");
        await DropAsync(db, cancellationToken);
        return "integer, integer beyond 2^53, real, text, blob, null";
    }

    private async Task<string> BatchAsync(CancellationToken cancellationToken)
    {
        var db = await FreshAsync("batch.sqlite", cancellationToken);
        await db.ExecuteAsync("CREATE TABLE t (k TEXT PRIMARY KEY)", null, cancellationToken);
        await db.ExecuteBatchAsync([new SqliteStatement("INSERT INTO t VALUES ('a')"), new SqliteStatement("INSERT INTO t VALUES ('b')")], cancellationToken);
        await ExpectFailureAsync(() => db.ExecuteBatchAsync([new SqliteStatement("INSERT INTO t VALUES ('c')"), new SqliteStatement("INSERT INTO t VALUES ('a')")],
                                                            cancellationToken), "the duplicate key fails the batch");
        var keys = (await db.QueryAsync("SELECT k FROM t ORDER BY k", null, cancellationToken)).Rows.Select(r => r.GetString(0)).ToList();
        Expect(keys.SequenceEqual(["a", "b"]), "the failed batch left " + string.Join(",", keys));
        await DropAsync(db, cancellationToken);
        return "the failed batch left nothing";
    }

    private async Task<string> ReadOnlyAsync(CancellationToken cancellationToken)
    {
        await ExpectFailureAsync(() => engine.OpenAsync(Prefix + "missing.sqlite", SqliteAccess.ReadOnly, cancellationToken), "a missing base read only");
        var db = await FreshAsync("ro.sqlite", cancellationToken);
        await db.ExecuteAsync("CREATE TABLE t (k TEXT)", null, cancellationToken);
        await db.DisposeAsync();
        var read = await engine.OpenAsync(Prefix + "ro.sqlite", SqliteAccess.ReadOnly, cancellationToken);
        await ExpectFailureAsync(() => read.ExecuteAsync("INSERT INTO t VALUES ('x')", null, cancellationToken), "a write to a base read only");
        await DropAsync(read, cancellationToken);
        return "refused";
    }

    private async Task<string> KeepAsync(CancellationToken cancellationToken)
    {
        var db = await FreshAsync("keep.sqlite", cancellationToken);
        await db.ExecuteAsync("CREATE TABLE t (k TEXT)", null, cancellationToken);
        await db.ExecuteAsync("INSERT INTO t VALUES ('x')", null, cancellationToken);
        await ExpectFailureAsync(() => engine.DeleteAsync(Prefix + "keep.sqlite", cancellationToken), "the delete of an open base");
        await db.DisposeAsync();
        var again = await engine.OpenAsync(Prefix + "keep.sqlite", SqliteAccess.ReadWrite, cancellationToken);
        Expect((string?)(await again.QueryAsync("SELECT k FROM t", null, cancellationToken)).Scalar() == "x", "the row after a reopen");
        await DropAsync(again, cancellationToken);
        Expect(!await engine.ExistsAsync(Prefix + "keep.sqlite", cancellationToken), "the base after its delete");
        return "kept, then deleted";
    }

    private async Task<string> ParallelAsync(CancellationToken cancellationToken)
    {
        var db = await FreshAsync("parallel.sqlite", cancellationToken);
        await db.ExecuteAsync("CREATE TABLE t (n INTEGER)", null, cancellationToken);
        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => i % 4 == 0
            ? (Task)db.QueryAsync("SELECT count(*) FROM t", null, cancellationToken)
            : db.ExecuteAsync("INSERT INTO t VALUES ($n)", new Dictionary<string, object?> { ["$n"] = (long)i }, cancellationToken)));
        var count = (long)(await db.QueryAsync("SELECT count(*) FROM t", null, cancellationToken)).Scalar()!;
        Expect(count == 150, $"{count} rows, 150 expected");
        await DropAsync(db, cancellationToken);
        return "150 rows";
    }

    private async Task<string> DocumentsAsync(CancellationToken cancellationToken)
    {
        if (await engine.ExistsAsync(Prefix + "documents.sqlite", cancellationToken))
        {
            await engine.DeleteAsync(Prefix + "documents.sqlite", cancellationToken);
        }

        await using (var store = new SqliteDocumentStore(engine, Prefix + "documents.sqlite"))
        {
            await store.PutAsync("bookmarks", [new LocalDocument("a", "{\"n\":1}"), new LocalDocument("b", "{\"n\":2}")], cancellationToken);
            await store.PutAsync("settings", [new LocalDocument("a", "{}")], cancellationToken);
            await store.PutAsync("bookmarks", [new LocalDocument("a", "{\"n\":3}")], cancellationToken);
            await store.DeleteAsync("bookmarks", ["b"], cancellationToken);
        }

        await using (var reopened = new SqliteDocumentStore(engine, Prefix + "documents.sqlite"))
        {
            var bookmarks = await reopened.LoadAsync("bookmarks", cancellationToken);
            Expect(bookmarks.SequenceEqual([new LocalDocument("a", "{\"n\":3}")]), "the bookmarks after a reopen");
            Expect((await reopened.LoadAsync("settings", cancellationToken)).Count == 1, "the settings apart");
        }

        await engine.DeleteAsync(Prefix + "documents.sqlite", cancellationToken);
        return "kept apart, replaced, deleted";
    }

    private async Task<string> CompareAsync(CancellationToken cancellationToken)
    {
        var store = compareWith!;
        const string Collection = Prefix + "load";
        var documents = Enumerable.Range(0, 10_000).Select(i => new LocalDocument($"k{i}", $"{{\"title\":\"Bookmark {i}\",\"note\":\"{new string('x', 400)}\"}}")).ToList();
        var clock = Stopwatch.StartNew();
        await store.PutAsync(Collection, documents, cancellationToken);
        var written = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        var read = await store.LoadAsync(Collection, cancellationToken);
        var loaded = clock.Elapsed.TotalMilliseconds;
        await store.DeleteAsync(Collection, [.. documents.Select(document => document.Key)], cancellationToken);
        Expect(read.Count == 10_000, $"{read.Count} documents read back");
        return $"put 10000 in {written:0} ms, load in {loaded:0} ms";
    }

    private async Task<string> LoadAsync(CancellationToken cancellationToken)
    {
        if (await engine.ExistsAsync(Prefix + "load.sqlite", cancellationToken))
        {
            await engine.DeleteAsync(Prefix + "load.sqlite", cancellationToken);
        }

        var documents = Enumerable.Range(0, 10_000).Select(i => new LocalDocument($"k{i}", $"{{\"title\":\"Bookmark {i}\",\"note\":\"{new string('x', 400)}\"}}")).ToList();
        var clock = Stopwatch.StartNew();
        string message;
        await using (var store = new SqliteDocumentStore(engine, Prefix + "load.sqlite"))
        {
            await store.PutAsync("bookmarks", documents, cancellationToken);
            var written = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            var read = await store.LoadAsync("bookmarks", cancellationToken);
            var loaded = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            await Task.WhenAll(Enumerable.Range(0, 100).Select(i => store.PutAsync("settings", [new LocalDocument($"s{i}", "{}")], cancellationToken).AsTask()));
            var small = clock.Elapsed.TotalMilliseconds;
            Expect(read.Count == 10_000, $"{read.Count} documents read back");
            message = $"put 10000 in {written:0} ms, load in {loaded:0} ms, 100 small puts in {small:0} ms";
        }

        await engine.DeleteAsync(Prefix + "load.sqlite", cancellationToken);
        return message;
    }
}
