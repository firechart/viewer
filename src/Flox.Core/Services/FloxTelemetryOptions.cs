namespace Flox.Core.Services;

// Should be bound to the section "Flox:OpenTelemetry"
public class FloxTelemetryOptions
{
    public bool Enabled { get; set; }
    public string? OtlpEndpoint { get; set; }

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
