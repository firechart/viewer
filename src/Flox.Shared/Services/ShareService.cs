using Microsoft.JSInterop;

namespace Flox.Shared.Services;

/// <summary>The outcome of an attempt to share text.</summary>
public enum ShareResult
{
    /// <summary>The text was passed to the sharing dialog.</summary>
    Shared,

    /// <summary>The user dismissed the sharing dialog, or another one is open.</summary>
    Cancelled,

    /// <summary>The sharing dialog could not be opened (no click of the user, not allowed here).</summary>
    Failed,

    /// <summary>The browser or the device has no sharing dialog.</summary>
    Unsupported
}

/// <summary>Shares text through the sharing dialog of the browser or the device, and copies text to the clipboard.</summary>
[FloxService]
public sealed class ShareService
(
    IJSRuntime js
) : IAsyncDisposable
{
    private Task<IJSObjectReference>? _module;

    // One import for every caller, also those that come before it is done
    private Task<IJSObjectReference> Module => _module ??= js.ImportModuleAsync("./_content/Flox.Shared/share.js").AsTask();

    /// <summary>Loads the script ahead, so the sharing dialog opens within the click of the user; tells whether there is such a dialog.</summary>
    public async Task<bool> PrepareAsync()
    {
        return await (await Module).InvokeAsync<bool>("canShare");
    }

    /// <summary>Shares the text under the title and reports what happened; call it from a click, after <see cref="PrepareAsync"/>.</summary>
    public async Task<ShareResult> ShareAsync(string title, string text)
    {
        var result = await (await Module).InvokeAsync<string>("share", title, text);

        return result switch
        {
            "shared" => ShareResult.Shared,
            "cancelled" => ShareResult.Cancelled,
            "failed" => ShareResult.Failed,
            var _ => ShareResult.Unsupported
        };
    }

    /// <summary>Copies the text to the clipboard.</summary>
    /// <returns>Whether the text was copied.</returns>
    public async Task<bool> CopyAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return await (await Module).InvokeAsync<bool>("copy", text);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await (await _module).DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
