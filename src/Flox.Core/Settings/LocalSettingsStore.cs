using Flox.Core.Data;

namespace Flox.Core.Settings;

/// <summary>The settings of a client without an application server, kept on the device: one document per key, in the store of the application or, for the keys a module routes, in the store of the module. A change made in another tab is heard through the stores.</summary>
public sealed class LocalSettingsStore : ISettingsStore, IDisposable
{
    /// <summary>The name of the collection that holds the settings.</summary>
    public const string Collection = "settings";

    private readonly ILocalDocumentStore _documents;
    private readonly Func<string, ILocalDocumentStore?> _route;
    private readonly List<ILocalDocumentStore> _stores;

    /// <summary>Creates the store.</summary>
    /// <param name="documents">The store of the application.</param>
    /// <param name="route">Picks the store of a key; null means the store of the application.</param>
    /// <param name="routed">The stores that <paramref name="route"/> can return, so changes made in them are heard.</param>
    public LocalSettingsStore(ILocalDocumentStore documents, Func<string, ILocalDocumentStore?>? route = null, IEnumerable<ILocalDocumentStore>? routed = null)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _route = route ?? (_ => null);
        _stores = [documents, .. (routed ?? []).Where(store => !ReferenceEquals(store, documents)).Distinct()];
        foreach (var store in _stores)
        {
            store.ChangedElsewhere += OnChangedElsewhere;
        }
    }

    /// <inheritdoc />
    public event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var documents = await StoreOf(key).LoadAsync(Collection, cancellationToken);
        return documents.FirstOrDefault(row => row.Key == key)?.Json;
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string? json, CancellationToken cancellationToken = default)
    {
        var store = StoreOf(key);
        if (json is null)
        {
            await store.DeleteAsync(Collection, [key], cancellationToken);
        }
        else
        {
            await store.PutAsync(Collection, [new LocalDocument(key, json)], cancellationToken);
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(key));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var store in _stores)
        {
            store.ChangedElsewhere -= OnChangedElsewhere;
        }
    }

    private ILocalDocumentStore StoreOf(string key)
    {
        return _route(key) ?? _documents;
    }

    // The store tells the collection, not the key: every key there is told
    private void OnChangedElsewhere(object? sender, LocalDocumentsChangedEventArgs e)
    {
        if (e.Concerns(Collection) && sender is ILocalDocumentStore store)
        {
            _ = TellEveryKeyAsync(store);
        }
    }

    private async Task TellEveryKeyAsync(ILocalDocumentStore store)
    {
        try
        {
            foreach (var document in await store.LoadAsync(Collection))
            {
                Changed?.Invoke(this, new SettingsChangedEventArgs(document.Key));
            }
        }
        catch (InvalidOperationException)
        {
            // The store is gone with the page
        }
    }
}
