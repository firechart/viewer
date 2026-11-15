namespace Flox.Core.Services;

/// <summary>The license of one third-party package, as listed in the licenses file of an application.</summary>
public sealed class LicenseEntry
{
    /// <summary>The id of the package.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("PackageId")]
    public string PackageId { get; set; } = string.Empty;

    /// <summary>The version of the package.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("PackageVersion")]
    public string PackageVersion { get; set; } = string.Empty;

    /// <summary>The address of the project of the package.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("PackageProjectUrl")]
    public string? PackageProjectUrl { get; set; }

    /// <summary>The authors of the package.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("Authors")]
    public string? Authors { get; set; }

    /// <summary>The license of the package, by its expression or name.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("License")]
    public string? License { get; set; }

    /// <summary>The address of the license text.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("LicenseUrl")]
    public string? LicenseUrl { get; set; }
}
