using FireChart.Viewer.Core;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireChart.Viewer;

/// <summary>
/// The <see cref="IRenderYield"/> of a browser: the rest of the work becomes a task of its own (a message of a <c>MessageChannel</c>), so the browser
/// may draw a frame before it. Not a timer: the timers of a hidden tab run at most once a second. Outside a browser (MAUI) it does nothing; scoped.
/// </summary>
/// <param name="js">The JavaScript of the page.</param>
/// <example>
/// <code>
/// await renderYield.YieldAsync();   // the busy indicator turns on
/// </code>
/// </example>
[FloxService(typeof(IRenderYield), ServiceLifetime.Scoped)]
public sealed class BrowserRenderYield(IJSRuntime js) : IRenderYield
{
    private IJSObjectReference? _module;

    /// <inheritdoc/>
    public async ValueTask YieldAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/FireChart.Viewer/js/render-yield.js");
        await _module.InvokeVoidAsync("yieldToBrowser");
    }
}
