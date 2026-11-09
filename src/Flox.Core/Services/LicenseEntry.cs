namespace Flox.Core.Services;

public sealed class LicenseEntry
{
    [System.Text.Json.Serialization.JsonPropertyName("PackageId")]
    public string PackageId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("PackageVersion")]
    public string PackageVersion { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("PackageProjectUrl")]
    public string? PackageProjectUrl { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("Authors")]
    public string? Authors { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("License")]
    public string? License { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("LicenseUrl")]
    public string? LicenseUrl { get; set; }
}
