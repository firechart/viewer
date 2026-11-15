using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Flox.Core.Serialization;

namespace Flox.Core.Util;

/// <summary>
/// Shared JSON serializer options and helpers.
/// </summary>
public static class JsonUtil
{
    /// <summary>
    /// Shared camel-case options meant to skip null values; currently built with the same settings as DefaultCamelCaseOptions.
    /// </summary>
    public static JsonSerializerOptions DefaultCamelCaseIgnoreNullOptions { get; } = CreateNewDefaultCamelCaseOptions(false);

    /// <summary>
    /// Shared camel-case options: tolerant reading, enums as strings, nulls written.
    /// </summary>
    public static JsonSerializerOptions DefaultCamelCaseOptions { get; } = CreateNewDefaultCamelCaseOptions(false);

    private static JsonSerializerOptions _jso = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Copies the settings and converters of one set of options to another, optionally putting an extra resolver in front of the resolver.
    /// </summary>
    public static void ApplyTo(JsonSerializerOptions source, JsonSerializerOptions target, IJsonTypeInfoResolver? extraResolver = null)
    {
        target.AllowTrailingCommas = source.AllowTrailingCommas;
        target.DefaultIgnoreCondition = source.DefaultIgnoreCondition;
        target.PropertyNameCaseInsensitive = source.PropertyNameCaseInsensitive;
        target.PreferredObjectCreationHandling = source.PreferredObjectCreationHandling;
        target.PropertyNamingPolicy = source.PropertyNamingPolicy;
        target.ReadCommentHandling = source.ReadCommentHandling;
        target.RespectNullableAnnotations = source.RespectNullableAnnotations;
        target.UnmappedMemberHandling = source.UnmappedMemberHandling;
        target.WriteIndented = source.WriteIndented;
        foreach (var converter in source.Converters)
        {
            target.Converters.Add(converter);
        }

        target.TypeInfoResolver = extraResolver is null ? source.TypeInfoResolver : JsonTypeInfoResolver.Combine(extraResolver, source.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());
    }

    /// <summary>
    /// Formats JSON text with indentation; text that is not valid JSON is returned as is, an empty one gives an empty string.
    /// </summary>
    public static string Beautify(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json,
                                                    new JsonDocumentOptions
                                                    {
                                                        AllowTrailingCommas = true,
                                                        CommentHandling = JsonCommentHandling.Skip
                                                    });

            return JsonSerializer.Serialize(document.RootElement, _jso);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    /// <summary>
    /// Creates options for reading files written by hand: tolerant to comments and trailing commas, strict about unknown members.
    /// </summary>
    public static JsonSerializerOptions CreateLaxOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = true,
            Converters =
            {
                new JsonStringEnumConverter(), new JsonElementConverter(), new StringListConverter()
            },
            PropertyNameCaseInsensitive = true,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            ReadCommentHandling = JsonCommentHandling.Skip,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = false // DONT CHANGE IT PLEASE
        };
        return options;
    }

    /// <summary>
    /// Creates new camel-case options: tolerant reading, enums as strings, unknown members skipped.
    /// </summary>
    public static JsonSerializerOptions CreateNewDefaultCamelCaseOptions(bool ignoreNulls = false)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = true,
            Converters =
            {
                new JsonStringEnumConverter(), new JsonElementConverter(), new StringListConverter() // Used by FHIR-to-entities mapper to resolve conflicts between STU3 and higher releases
            },
            PropertyNameCaseInsensitive = true,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            WriteIndented = false,
            DefaultIgnoreCondition = ignoreNulls ? JsonIgnoreCondition.WhenWritingNull : JsonIgnoreCondition.Never
        };
        return options;
    }

    /// <summary>
    /// Deserializes a JSON element with the shared options; returns null when it does not fit the type.
    /// </summary>
    public static T? DeserializeFromJsonElement<T>(JsonElement value) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(value, DefaultCamelCaseIgnoreNullOptions);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Serializes the value with the shared camel-case options.
    /// </summary>
    public static string Serialize<T>(T value, bool ignoreNulls = false)
    {
        if (ignoreNulls)
        {
            return JsonSerializer.Serialize(value, DefaultCamelCaseIgnoreNullOptions);
        }
        else
        {
            return JsonSerializer.Serialize(value, DefaultCamelCaseOptions);
        }
    }

    /// <summary>
    /// Converts an object to a JSON element; null gives an undefined element.
    /// </summary>
    public static JsonElement SerializeToJsonElement(object? obj)
    {
        if (obj == null)
        {
            return new JsonElement();
        }

        var json = JsonSerializer.Serialize(obj, DefaultCamelCaseOptions);
        var document = JsonDocument.Parse(json);
        return document.RootElement;
    }

    /// <summary>
    /// Serializes the object with the shared camel-case options; null gives an empty string.
    /// </summary>
    public static string ToJson(object? obj)
    {
        return obj == null ? string.Empty : JsonSerializer.Serialize(obj, DefaultCamelCaseOptions);
    }
}
