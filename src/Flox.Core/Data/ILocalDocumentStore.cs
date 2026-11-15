namespace Flox.Core.Data;

/// <summary>
/// The documents kept on the device, by key inside a collection (SQLite in a browser); <c>LocalEntityStorage</c> queries them in memory.
/// </summary>
public interface ILocalDocumentStore
{
    /// <summary>Reads all the documents of the collection.</summary>
    ValueTask<IReadOnlyList<LocalDocument>> LoadAsync(string collection, CancellationToken cancellationToken = default);

    /// <summary>Writes the documents in one transaction: all of them or none.</summary>
    ValueTask PutAsync(string collection, IReadOnlyCollection<LocalDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>Deletes the documents with the keys in one transaction; a missing key is not an error.</summary>
    ValueTask DeleteAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when another tab of the same site changed a collection (null: any may have changed, the tab was asleep), so that what was read of
    /// it is stale. Never raised for the own writes.
    /// </summary>
    event EventHandler<LocalDocumentsChangedEventArgs>? ChangedElsewhere;
}

/// <summary>A document of a local store: its key and its JSON text.</summary>
/// <param name="Key">The key of the document inside its collection.</param>
/// <param name="Json">The document as JSON.</param>
public sealed record LocalDocument(string Key, string Json);

/// <summary>Tells which collection another tab changed.</summary>
/// <param name="collection">The collection that changed; null for all of them.</param>
public sealed class LocalDocumentsChangedEventArgs(string? collection) : EventArgs
{
    /// <summary>The collection that changed; null for all of them.</summary>
    public string? Collection { get; } = collection;

    /// <summary>Tells whether the change concerns the collection.</summary>
    public bool Concerns(string collection)
    {
        return Collection is null || Collection == collection;
    }
}

/// <summary>A storage of the device that learns that another tab changed its rows: a list of them reads again.</summary>
public interface ILocalChangeNotifier
{
    /// <summary>Raised when another tab changed the rows of the storage.</summary>
    event EventHandler? ChangedElsewhere;
}

/// <summary>The documents in the memory of the process: tests, and a host without a store of its own (they are gone with the process).</summary>
public sealed class InMemoryDocumentStore : ILocalDocumentStore
{
    private readonly Dictionary<string, Dictionary<string, string>> _collections = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <summary>Never raised: one process has no other tab.</summary>
    public event EventHandler<LocalDocumentsChangedEventArgs>? ChangedElsewhere
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LocalDocument>> LoadAsync(string collection, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<LocalDocument> result = _collections.TryGetValue(collection, out var documents) ? [.. documents.Select(row => new LocalDocument(row.Key, row.Value))] : [];
            return ValueTask.FromResult(result);
        }
    }

    /// <inheritdoc />
    public ValueTask PutAsync(string collection, IReadOnlyCollection<LocalDocument> documents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        lock (_lock)
        {
            if (!_collections.TryGetValue(collection, out var stored))
            {
                stored = new Dictionary<string, string>(StringComparer.Ordinal);
                _collections[collection] = stored;
            }

            foreach (var document in documents)
            {
                stored[document.Key] = document.Json;
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (_lock)
        {
            if (_collections.TryGetValue(collection, out var stored))
            {
                foreach (var key in keys)
                {
                    stored.Remove(key);
                }
            }
        }

        return ValueTask.CompletedTask;
    }
}
