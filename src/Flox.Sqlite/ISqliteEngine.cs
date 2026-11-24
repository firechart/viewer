namespace Flox.Sqlite;

/// <summary>
/// Where the SQLite bases of a client live, by name (flox.sqlite, firechart.sqlite, the base of the packages): in the browser sqlite-wasm in a
/// worker over OPFS, elsewhere files through Microsoft.Data.Sqlite. Every call is async: in the browser it crosses into the worker.
/// </summary>
public interface ISqliteEngine
{
    /// <summary>
    /// Opens the base; a base that does not exist is created for <see cref="SqliteAccess.ReadWrite"/>, <see cref="SqliteAccess.ReadOnly"/>
    /// refuses it.
    /// </summary>
    Task<ISqliteDatabase> OpenAsync(string name, SqliteAccess access, CancellationToken cancellationToken = default);

    /// <summary>Tells whether the base exists.</summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes the base, which must not be open.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Returns the names of the bases.</summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a base from an absolute address, compressed with gzip, and writes it as a new name; the name is there only when the base is
    /// complete, a failure or a cancellation leaves nothing; until then the name cannot be opened, deleted or imported again. The progress counts
    /// the compressed bytes received. Returns the size of the base in bytes.
    /// </summary>
    Task<long> ImportAsync(string name, Uri source, IProgress<long>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>How a base is opened.</summary>
public enum SqliteAccess
{
    /// <summary>Reading only.</summary>
    ReadOnly,
    /// <summary>Reading and writing.</summary>
    ReadWrite
}

/// <summary>
/// An open base. The values of rows are null, long, double, string or byte[]; a parameter is named with its prefix (<c>$name</c>) and its
/// value is one of those or what <see cref="SqliteValues.Normalize"/> turns into one. A cancellation ends the wait of the caller, not a
/// statement already sent: a cancelled write may have been done. In the browser a write is done at least once: when the tab that runs the
/// bases closes before it answers, the statement is sent again to the next one, so a statement should bear being repeated (an upsert, a
/// delete by key).
/// </summary>
public interface ISqliteDatabase : IAsyncDisposable
{
    /// <summary>The name of the base.</summary>
    string Name { get; }

    /// <summary>How the base was opened.</summary>
    SqliteAccess Access { get; }

    /// <summary>Runs a query and returns all of its rows.</summary>
    Task<SqliteRows> QueryAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Runs a statement and returns the number of rows it changed.</summary>
    Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the statements in one transaction (in the browser one trip to the worker): all of them or none. Returns the rows each statement
    /// changed.
    /// </summary>
    Task<IReadOnlyList<int>> ExecuteBatchAsync(IReadOnlyList<SqliteStatement> statements, CancellationToken cancellationToken = default);
}

/// <summary>A statement with its parameters, for a batch.</summary>
/// <param name="Sql">The SQL text.</param>
/// <param name="Parameters">The parameters by their prefixed names, if any.</param>
public sealed record SqliteStatement(string Sql, IReadOnlyDictionary<string, object?>? Parameters = null);

/// <summary>A failure of SQLite (the code and the message of the engine), the same from both engines.</summary>
/// <param name="message">The message of the engine.</param>
/// <param name="code">The result code of SQLite, zero when unknown.</param>
/// <param name="inner">The cause, if any.</param>
public sealed class SqliteEngineException(string message, int code = 0, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>The result code of SQLite, zero when unknown.</summary>
    public int Code { get; } = code;
}
