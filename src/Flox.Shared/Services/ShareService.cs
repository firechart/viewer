using Microsoft.JSInterop;

namespace Flox.Shared.Services;

/// <summary>The outcome of an attempt to share text.</summary>
public enum ShareResult
{
    /// <summary>The text was passed to the sharing dialog.</summary>
    Shared,

    /// <summary>The text was copied to the clipboard instead.</summary>
    Copied,

    /// <summary>The user dismissed the sharing dialog.</summary>
    Cancelled,

    /// <summary>Neither sharing nor copying is available.</summary>
    Unsupported
}

/// <summary>Shares text through the sharing dialog of the browser or the device, or copies it where sharing is not available.</summary>
[FloxService]
public sealed class ShareService
(
    IJSRuntime js
) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <summary>Shares the text under the title and reports what happened.</summary>
    public async Task<ShareResult> ShareAsync(string title, string text)
    {
        _module ??= await js.ImportModuleAsync("./_content/Flox.Shared/share.js");

        var result = await _module.InvokeAsync<string>("share", title, text);

        return result switch
        {
            "shared" => ShareResult.Shared,
            "copied" => ShareResult.Copied,
            "cancelled" => ShareResult.Cancelled,
            var _ => ShareResult.Unsupported
        };
    }

    /// <summary>Tells whether the browser or the device has a sharing dialog.</summary>
    public async Task<bool> CanShareAsync()
    {
        _module ??= await js.ImportModuleAsync("./_content/Flox.Shared/share.js");

        return await _module.InvokeAsync<bool>("canShare");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
