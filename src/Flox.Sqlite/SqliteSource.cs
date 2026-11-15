
namespace Flox.Sqlite;

/// <summary>
/// A base of the <see cref="ISqliteEngine"/> that the storages read, opened read only at the first need and checked once (PRAGMA user_version). The name is asked
/// for at every opening: a base that is not there (none chosen, not downloaded yet) reads as empty; <see cref="ResetAsync"/> closes it, so a replaced base is opened again.
/// </summary>
/// <param name="engine">The engine that holds the bases.</param>
/// <param name="name">Gives the name of the base to open, or null when none is chosen.</param>
/// <param name="schemaVersion">The schema version (PRAGMA user_version) this reader reads.</param>
public sealed class SqliteSource(ISqliteEngine engine, Func<CancellationToken, Task<string?>> name, int schemaVersion) : IAsyncDisposable
{
    /// <summary>
    /// The code of the <see cref="SqliteEngineException"/> of a base of another schema (not one of SQLite).
    /// </summary>
    public const int OtherSchema = -1;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ISqliteDatabase? _database;
    private bool _disposed;

    /// <summary>
    /// Creates a source over the base of a fixed name.
    /// </summary>
    /// <param name="engine">The engine that holds the bases.</param>
    /// <param name="name">The name of the base.</param>
    /// <param name="schemaVersion">The schema version (PRAGMA user_version) this reader reads.</param>
    public SqliteSource(ISqliteEngine engine, string name, int schemaVersion)
        : this(engine, _ => Task.FromResult<string?>(name), schemaVersion)
    {
    }

    /// <summary>
    /// Gets the open base; null when there is none.
    /// </summary>
    /// <exception cref="SqliteEngineException">The base is of another schema (<see cref="OtherSchema"/>) or cannot be opened.</exception>
    public async Task<ISqliteDatabase?> DatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (_database is not null)
        {
            return _database;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_database is null && await name(cancellationToken) is { } current && await engine.ExistsAsync(current, cancellationToken))
            {
                var database = await engine.OpenAsync(current, SqliteAccess.ReadOnly, cancellationToken);
                var version = Convert.ToInt32((await database.QueryAsync("PRAGMA user_version", null, cancellationToken)).Scalar(), System.Globalization.CultureInfo.InvariantCulture);
                if (version != schemaVersion)
                {
                    await database.DisposeAsync();
                    throw new SqliteEngineException($"{current} is a base of the schema {version}; this reader reads {schemaVersion}.", OtherSchema);
                }

                _database = database;
            }

            return _database;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Closes the base, so that the next read opens it again.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_database is not null)
            {
                await _database.DisposeAsync();
                _database = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await ResetAsync();
        _disposed = true;
        _gate.Dispose();
    }
}
