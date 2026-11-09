using Microsoft.JSInterop;

namespace Flox.Shared.Services;

public sealed class TelemetryConsentBanner(IJSRuntime js, Func<bool, Task> onChanged) : IAsyncDisposable
{
    private DotNetObjectReference<TelemetryConsentBanner>? _reference;

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

        await js.InvokeVoidAsync("floxConsent.init", _reference, texts, allowed);
    }

    public async Task SetAsync(bool allowed)
    {
        if (_reference is not null)
        {
            await js.InvokeVoidAsync("floxConsent.setTelemetry", allowed);
        }
    }

    [JSInvokable]
    public Task OnTelemetryConsentChanged(bool allowed)
    {
        return onChanged(allowed);
    }

    public ValueTask DisposeAsync()
    {
        _reference?.Dispose();
        return ValueTask.CompletedTask;
    }
}
