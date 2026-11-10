using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Flox.Services;

// The documents on the device in IndexedDB of the browser (the WebView of MAUI too): one database "flox", one object store keyed by collection and key.
// The site data of the browser is the only copy: clearing it deletes them
[FloxService(typeof(ILocalDocumentStore), ServiceLifetime.Scoped)]
public sealed class IndexedDbDocumentStore(IJSRuntime runtime) : ILocalDocumentStore, IAsyncDisposable
{
    private const string ModulePath = "./_content/Flox.Shared/flox-documents.js";

    private IJSObjectReference? _module;

    public async ValueTask<IReadOnlyList<LocalDocument>> LoadAsync(string collection, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        var module = await ModuleAsync(cancellationToken);
        return await module.InvokeAsync<LocalDocument[]>("load", cancellationToken, collection);
    }

    public async ValueTask PutAsync(string collection, IReadOnlyCollection<LocalDocument> documents, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.Count == 0)
        {
            return;
        }

        var module = await ModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("put", cancellationToken, collection, documents);
    }

    public async ValueTask DeleteAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return;
        }

        var module = await ModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("remove", cancellationToken, collection, keys);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // the page is gone, and the module with it
            }
        }
    }

    private async ValueTask<IJSObjectReference> ModuleAsync(CancellationToken cancellationToken)
    {
        return _module ??= await runtime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
    }
}
