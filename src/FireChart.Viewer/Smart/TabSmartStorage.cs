using FireChart.Viewer.Core.Smart;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireChart.Viewer.Smart;

/// <summary>
/// The <see cref="ISmartStorage"/> of a browser tab (<c>sessionStorage</c>, or the storage of the tab a host has put aside as <c>window.__floxTabStorage</c>):
/// gone when the tab closes; scoped.
/// </summary>
/// <param name="js">The JavaScript of the page.</param>
/// <example>
/// <code>
/// await storage.SetAsync("fhir:session:42", json);
/// </code>
/// </example>
[FloxService(typeof(ISmartStorage), ServiceLifetime.Scoped)]
public sealed class TabSmartStorage(IJSRuntime js) : ISmartStorage
{
    private IJSObjectReference? _module;

    /// <inheritdoc/>
    public async Task<string?> GetAsync(string key)
    {
        return await (await ModuleAsync()).InvokeAsync<string?>("getItem", key);
    }

    /// <inheritdoc/>
    public async Task SetAsync(string key, string value)
    {
        await (await ModuleAsync()).InvokeVoidAsync("setItem", key, value);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key)
    {
        await (await ModuleAsync()).InvokeVoidAsync("removeItem", key);
    }

    private async Task<IJSObjectReference> ModuleAsync()
    {
        return _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/FireChart.Viewer/js/smart-storage.js");
    }
}
