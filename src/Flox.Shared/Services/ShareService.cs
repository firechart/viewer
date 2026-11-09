using Microsoft.JSInterop;

namespace Flox.Shared.Services;

public enum ShareResult { Shared, Copied, Cancelled, Unsupported }

[FloxService]
public sealed class ShareService
(
    IJSRuntime js
) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task<ShareResult> ShareAsync(string title, string text)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/Flox.Shared/share.js");

        var result = await _module.InvokeAsync<string>("share", title, text);

        return result switch
        {
            "shared" => ShareResult.Shared,
            "copied" => ShareResult.Copied,
            "cancelled" => ShareResult.Cancelled,
            var _ => ShareResult.Unsupported
        };
    }

    public async Task<bool> CanShareAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/Flox.Shared/share.js");

        return await _module.InvokeAsync<bool>("canShare");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
