namespace Flox.Core.Services;

/// <summary>Whether the user agreed to send logs and traces to the server. Denied until the settings are loaded, so nothing leaves the client before the answer is known.</summary>
public sealed class TelemetryConsent
{
    private volatile bool _allowed;

    /// <summary>Whether sending is allowed.</summary>
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
