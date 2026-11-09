using System.Text.Json;

namespace Flox.Core.Util;

// Turns a JSON document into the flat "a:b:0" keys of Microsoft.Extensions.Configuration, so arrays
// and nested objects (Flox:Oidc:Scopes, Flox:Oidc:AdditionalParameters) bind like they do from appsettings.json
public static class JsonConfigurationFlattener
{
    public static Dictionary<string, string?> Flatten(string json)
    {
        using var document = JsonDocument.Parse(json);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        FlattenElement(document.RootElement, null, values);
        return values;
    }

    private static void FlattenElement(JsonElement element, string? prefix, Dictionary<string, string?> target)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    FlattenElement(property.Value, prefix == null ? property.Name : $"{prefix}:{property.Name}", target);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenElement(item, $"{prefix}:{index++}", target);
                }

                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
            default:
                if (prefix != null)
                {
                    target[prefix] = element.ToString();
                }

                break;
        }
    }
}
