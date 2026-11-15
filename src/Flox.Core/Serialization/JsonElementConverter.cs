using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flox.Core.Serialization;

/// <summary>
/// Reads and writes a JsonElement as is; an undefined element is written as null.
/// </summary>
public sealed class JsonElementConverter : JsonConverter<JsonElement>
{
    /// <inheritdoc />
    public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.Clone();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
