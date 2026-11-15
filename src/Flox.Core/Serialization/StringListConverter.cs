using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flox.Core.Serialization;

/// <summary>
/// Reads a list of strings written either as one string (STU3) or as an array (R4 and later).
/// </summary>
public class StringListConverter : JsonConverter<List<string>>
{
    /// <inheritdoc />
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            // STU3: string
            return [reader.GetString()!];
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            // R4+: list of strings
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    list.Add(reader.GetString()!);
                }
            }

            return list;
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
