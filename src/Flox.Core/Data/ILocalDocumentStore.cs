namespace Flox.Core.Data;

// The documents kept on the device, by key inside a collection (IndexedDB in a browser); LocalEntityStorage queries them in memory
public interface ILocalDocumentStore
{
    ValueTask<IReadOnlyList<LocalDocument>> LoadAsync(string collection, CancellationToken cancellationToken = default);

    // One transaction: all the documents are written or none
    ValueTask PutAsync(string collection, IReadOnlyCollection<LocalDocument> documents, CancellationToken cancellationToken = default);

    // One transaction; a missing key is not an error
    ValueTask DeleteAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}

public sealed record LocalDocument(string Key, string Json);

// The documents in the memory of the process: tests, and a host without a store of its own (they are gone with the process)
public sealed class InMemoryDocumentStore : ILocalDocumentStore
{
    private readonly Dictionary<string, Dictionary<string, string>> _collections = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public ValueTask<IReadOnlyList<LocalDocument>> LoadAsync(string collection, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<LocalDocument> result = _collections.TryGetValue(collection, out var documents) ? [.. documents.Select(row => new LocalDocument(row.Key, row.Value))] : [];
            return ValueTask.FromResult(result);
        }
    }

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
