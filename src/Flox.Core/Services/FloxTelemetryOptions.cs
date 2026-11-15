namespace Flox.Core.Services;

/// <summary>Settings of the telemetry export, bound to the <c>Flox:OpenTelemetry</c> section.</summary>
public class FloxTelemetryOptions
{
    /// <summary>Whether telemetry is exported.</summary>
    public bool Enabled { get; set; }
    /// <summary>The address of the OTLP collector.</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>Checks that the options are complete when telemetry is on.</summary>
    /// <exception cref="InvalidOperationException">The endpoint is missing.</exception>
    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(OtlpEndpoint))
        {
            throw new InvalidOperationException("Flox:OpenTelemetry:Enabled is true but Flox:OpenTelemetry:OtlpEndpoint is missing. " + "Either configure it or set Flox:OpenTelemetry:Enabled=false.");
        }
    }
}
