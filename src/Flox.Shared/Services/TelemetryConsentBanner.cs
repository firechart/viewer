using Microsoft.JSInterop;

namespace Flox.Shared.Services;

/// <summary>The banner of the telemetry consent (cookie-gdpr.js, imported with its library at the first show): shown only where telemetry is on.</summary>
public sealed class TelemetryConsentBanner(IJSRuntime js, Func<bool, Task> onChanged) : IAsyncDisposable
{
    private const string ModulePath = "./_content/Flox.Shared/cookie-gdpr.js";

    private DotNetObjectReference<TelemetryConsentBanner>? _reference;
    private IJSObjectReference? _module;

    /// <summary>Shows the banner with the current choice; does nothing when a content blocker refuses the library.</summary>
    public async Task ShowAsync(bool allowed)
    {
        _reference = DotNetObjectReference.Create(this);
        var texts = new
        {
            title = Strings.Consent_Title,
            description = Strings.Consent_Description,
            accept = Strings.Consent_Accept,
            reject = Strings.Consent_Reject,
            manage = Strings.Consent_Manage,
            preferencesTitle = Strings.Consent_PreferencesTitle,
            save = Strings.Consent_Save,
            close = Strings.Consent_Close,
            necessaryTitle = Strings.Consent_NecessaryTitle,
            necessaryDescription = Strings.Consent_NecessaryDescription,
            telemetryTitle = Strings.Consent_TelemetryTitle,
            telemetryDescription = Strings.Consent_TelemetryDescription,
        };

        try
        {
            _module = await js.ImportModuleAsync(ModulePath);
        }
        catch (JSException)
        {
            return;
        }

        await InvokeAsync("init", _reference, texts, allowed);
    }

    /// <summary>Sets the telemetry choice shown by the banner.</summary>
    public async Task SetAsync(bool allowed)
    {
        if (_module is not null)
        {
            await InvokeAsync("setTelemetry", allowed);
        }
    }

    // The library may be refused by a content blocker: no banner, the setting stays the source of truth
    private async Task InvokeAsync(string identifier, params object?[] args)
    {
        try
        {
            await _module!.InvokeVoidAsync(identifier, args);
        }
        catch (JSException)
        {
        }
    }

    /// <summary>Called by the banner script when the user changes the choice; passes it on to the owner.</summary>
    [JSInvokable]
    public Task OnTelemetryConsentChanged(bool allowed)
    {
        return onChanged(allowed);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _reference?.Dispose();
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
