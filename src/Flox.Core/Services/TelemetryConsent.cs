namespace Flox.Core.Services;

// Whether the user agreed to send logs and traces to the server (GeneralAppSettings.AllowTelemetry).
// Denied until the settings are loaded, so nothing leaves the client before the answer is known
public sealed class TelemetryConsent
{
    private volatile bool _allowed;

    public bool Allowed
    {
        get
        {
            return _allowed;
        }

        set
        {
            _allowed = value;
        }
    }
}
