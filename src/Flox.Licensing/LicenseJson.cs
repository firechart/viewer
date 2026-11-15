using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flox.Licensing;

/// <summary>
/// The JSON form of a license: camelCase, enums as camelCase strings, days as yyyy-MM-dd.
/// </summary>
public static class LicenseJson
{
    /// <summary>The indented options used to write license files.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>The same options without indentation, for the JWT parts.</summary>
    public static readonly JsonSerializerOptions Compact = new(Options) { WriteIndented = false };

    /// <summary>Writes a license as indented JSON.</summary>
    public static string Serialize<T>(T license) where T : License
    {
        return JsonSerializer.Serialize(license, Options);
    }

    /// <summary>Reads a license from JSON.</summary>
    /// <exception cref="LicenseException">The JSON is empty or broken.</exception>
    public static T Deserialize<T>(string json) where T : License
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? throw new LicenseException("The license JSON is empty");
        }
        catch (JsonException e)
        {
            throw new LicenseException("The license JSON is broken: " + e.Message);
        }
    }

    /// <summary>Gets the name of a license kind as it is written in JSON and in the token.</summary>
    public static string KindName(LicenseKind kind)
    {
        return JsonNamingPolicy.CamelCase.ConvertName(kind.ToString());
    }
}
