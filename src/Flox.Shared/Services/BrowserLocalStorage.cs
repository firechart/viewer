using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Flox.Services;

// localStorage of the browser; public: the Viewer keeps the last search of a list in it
[FloxService(typeof(ILocalStorage), ServiceLifetime.Scoped)]
public class BrowserLocalStorage : ILocalStorage
{
    private readonly IJSRuntime _runtime;
    private readonly ILogger<BrowserLocalStorage> _logger;

    public BrowserLocalStorage(IJSRuntime runtime, ILogger<BrowserLocalStorage> logger)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string StorageName
    {
        get
        {
            return nameof(BrowserLocalStorage);
        }
    }

    public Task<List<string>> GetAllKeysAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetAsync(string key)
    {
        var result = await _runtime.InvokeAsync<string?>("localStorage.getItem", key);
        _logger.LogDebug("Read from localStorage OK: Key={Key}", key);
        return result;
    }

    public async Task RemoveAsync(string key)
    {
        await _runtime.InvokeVoidAsync("localStorage.removeItem", key);
        _logger.LogDebug("Remove from localStorage OK: Key={Key}", key);
    }

    public async Task SetAsync(string key, string? value)
    {
        if (value == null)
        {
            await RemoveAsync(key);
            return;
        }

        await _runtime.InvokeVoidAsync("localStorage.setItem", key, value);
        _logger.LogDebug("Write to localStorage OK: Key={Key}", key);
    }
}
