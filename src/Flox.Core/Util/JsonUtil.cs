using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Flox.Core.Serialization;

namespace Flox.Core.Util;

public static class JsonUtil
{
    public static JsonSerializerOptions DefaultCamelCaseIgnoreNullOptions { get; } = CreateNewDefaultCamelCaseOptions(false);

    public static JsonSerializerOptions DefaultCamelCaseOptions { get; } = CreateNewDefaultCamelCaseOptions(false);

    private static JsonSerializerOptions _jso = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

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

    public static string ToJson(object? obj)
    {
        return obj == null ? string.Empty : JsonSerializer.Serialize(obj, DefaultCamelCaseOptions);
    }
}
