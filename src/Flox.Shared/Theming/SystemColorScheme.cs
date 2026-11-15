using Microsoft.JSInterop;

namespace Flox.Shared.Theming;

/// <summary>
/// The color scheme of the system (<c>prefers-color-scheme</c>) for the theme <see cref="Theme.System"/>: whether it is dark now, and a change as it happens.
/// </summary>
public sealed class SystemColorScheme(IJSRuntime js, Func<bool, Task> changed) : IAsyncDisposable
{
    private const string ModulePath = "./_content/Flox.Shared/flox-color-scheme.js";

    private readonly string _id = Guid.NewGuid().ToString("N");
    private IJSObjectReference? _module;
    private DotNetObjectReference<SystemColorScheme>? _self;

    /// <summary>
    /// Starts to follow the system and returns whether its scheme is dark; false where the browser cannot tell (prerendering, a failed module).
    /// </summary>
    public async Task<bool> WatchAsync()
    {
        try
        {
            _module ??= await js.ImportModuleAsync(ModulePath);
            _self ??= DotNetObjectReference.Create(this);
            return await _module.InvokeAsync<bool>("watch", _id, _self);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Called by the browser when the scheme of the system changes.
    /// </summary>
    /// <param name="dark">Whether the scheme is dark now.</param>
    [JSInvokable]
    public Task OnSystemDarkChanged(bool dark)
    {
        return changed(dark);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("stop", _id);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // the page is gone, and its listener with it
            }
        }

        _self?.Dispose();
    }
}
