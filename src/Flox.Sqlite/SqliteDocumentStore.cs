using System.Text;
using System.Text.Json;
using Flox.Core.Data;

namespace Flox.Sqlite;

/// <summary>
/// The documents of the device in one SQLite base of the client: one table keyed by the collection and the key, the JSON as it is. No schema of
/// the entities, so a changed entity needs no migration. The base is opened at the first use.
/// </summary>
public sealed class SqliteDocumentStore : ILocalDocumentStore, IAsyncDisposable
{
    private const string Schema = "CREATE TABLE IF NOT EXISTS documents (collection TEXT NOT NULL, key TEXT NOT NULL, json TEXT NOT NULL, " +
                                  "PRIMARY KEY (collection, key)) WITHOUT ROWID";

    private readonly ISqliteEngine _engine;
    private readonly string _name;
    private readonly Lock _lock = new();
    private Task<ISqliteDatabase>? _opening;

    /// <summary>Creates the store over the base with the name.</summary>
    public SqliteDocumentStore(ISqliteEngine engine, string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _name = databaseName;
    }

    /// <summary>Never raised: a write of another tab is seen after a reload (the tabs share the base through the leading tab, not their rows).</summary>
    public event EventHandler<LocalDocumentsChangedEventArgs>? ChangedElsewhere
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<LocalDocument>> LoadAsync(string collection, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        var database = await DatabaseAsync(cancellationToken);
        var rows = await database.QueryAsync("SELECT key, json FROM documents WHERE collection = $c", new Dictionary<string, object?> { ["$c"] = collection },
                                             cancellationToken);
        return [.. rows.Rows.Select(row => new LocalDocument(row.GetString(0), row.GetString(1)))];
    }

    /// <inheritdoc />
    public async ValueTask PutAsync(string collection, IReadOnlyCollection<LocalDocument> documents, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.Count == 0)
        {
            return;
        }

        // One statement over a JSON array, not a statement per document: one trip to the worker of the browser, one transaction
        var database = await DatabaseAsync(cancellationToken);
        await database.ExecuteAsync("INSERT OR REPLACE INTO documents (collection, key, json) SELECT $c, value ->> 'k', value ->> 'j' FROM json_each($d)",
                                    new Dictionary<string, object?> { ["$c"] = collection, ["$d"] = Array(documents) }, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return;
        }

        var database = await DatabaseAsync(cancellationToken);
        await database.ExecuteAsync("DELETE FROM documents WHERE collection = $c AND key IN (SELECT value FROM json_each($k))",
                                    new Dictionary<string, object?> { ["$c"] = collection, ["$k"] = JsonSerializer.Serialize(keys) }, cancellationToken);
    }

    /// <summary>Closes the base if it was opened.</summary>
    public async ValueTask DisposeAsync()
    {
        Task<ISqliteDatabase>? opening;
        lock (_lock)
        {
            opening = _opening;
            _opening = null;
        }

        if (opening is { IsCompletedSuccessfully: true })
        {
            await (await opening).DisposeAsync();
        }
    }

    // [{"k":key,"j":json},...]
    private static string Array(IReadOnlyCollection<LocalDocument> documents)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var document in documents)
            {
                writer.WriteStartObject();
                writer.WriteString("k", document.Key);
                writer.WriteString("j", document.Json);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    // One opening for all the callers; a failed one is tried again by the next call
    private async Task<ISqliteDatabase> DatabaseAsync(CancellationToken cancellationToken)
    {
        Task<ISqliteDatabase> opening;
        lock (_lock)
        {
            opening = _opening ??= OpenAsync();
        }

        try
        {
            return await opening.WaitAsync(cancellationToken);
        }
        catch when (opening.IsFaulted)
        {
            lock (_lock)
            {
                if (_opening == opening)
                {
                    _opening = null;
                }
            }

            throw;
        }
    }

    private async Task<ISqliteDatabase> OpenAsync()
    {
        var database = await _engine.OpenAsync(_name, SqliteAccess.ReadWrite);
        await database.ExecuteAsync(Schema);
        return database;
    }
}
