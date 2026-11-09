using System.Collections.Concurrent;
using FireChart.Viewer.Core.Fhir;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace FireChart.Viewer.App;

// The assemblies of the releases other than R4 are not in the first download (BlazorWebAssemblyLazyLoad): the first server of a release fetches its own
internal sealed class WasmReleaseAssemblyLoader(LazyAssemblyLoader loader) : IReleaseAssemblyLoader
{
    private readonly ConcurrentDictionary<string, Task> _loading = new(StringComparer.Ordinal);

    public Task EnsureAsync(string release, CancellationToken cancellationToken = default)
    {
        return _loading.GetOrAdd(release, LoadAsync);
    }

    private async Task LoadAsync(string release)
    {
        try
        {
            await loader.LoadAssembliesAsync([$"Hl7.Fhir.{release}.wasm"]);
        }
        catch
        {
            _loading.TryRemove(release, out _);
            throw;
        }
    }
}
