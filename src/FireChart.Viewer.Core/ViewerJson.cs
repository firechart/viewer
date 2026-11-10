using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Core;

/// <summary>
/// How the files of the Viewer (search forms, summary forms) are read and written.
/// </summary>
/// <remarks>
/// <para>
/// One set of rules for every file: camelCase property names, enumerations as camelCase strings, the keys of dictionaries as they are (<c>ru-RU</c> stays
/// <c>ru-RU</c>), letters of any language unescaped. Reading is strict: a property the model does not have, a <see langword="null"/> where the model does not
/// allow one and a missing required property are errors. Writing is canonical: indented by two spaces, <c>\n</c> line ends, a final line end, the properties
/// in the order of the model, <see langword="null"/> left out, default values written (so a value that differs from the initializer is never lost). Reading
/// a file and writing it again gives the same text.
/// </para>
/// <para>
/// The metadata of the models are generated at build time, so trimming does not remove what the serializer needs.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var form = ViewerJson.Read&lt;SearchFormDefinition&gt;(File.ReadAllText("search-forms/R4/Patient.json"));
/// form.Fields[0].Order = 5;
/// File.WriteAllText("search-forms/R4/Patient.json", ViewerJson.Write(form));
/// </code>
/// </example>
public static class ViewerJson
{
    /// <summary>
    /// The options of the rules above, over the generated metadata of the models of the Viewer.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>
    /// Reads a file of the Viewer.
    /// </summary>
    /// <typeparam name="T">The model: <see cref="SearchFormDefinition"/> or <see cref="ClinicalSummaryFormDefinition"/>.</typeparam>
    /// <param name="json">The text of the file.</param>
    /// <returns>The model.</returns>
    /// <exception cref="JsonException">The text is not JSON, or breaks a rule of reading (an unknown property, a forbidden <see langword="null"/>, a missing
    /// required property); the message names the path.</exception>
    public static T Read<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("The file is the JSON null, not an object.");
    }

    /// <summary>
    /// Writes a file of the Viewer in the canonical form.
    /// </summary>
    /// <typeparam name="T">The model: <see cref="SearchFormDefinition"/> or <see cref="ClinicalSummaryFormDefinition"/>.</typeparam>
    /// <param name="value">The model.</param>
    /// <returns>The text of the file, with a final line end.</returns>
    public static string Write<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options) + "\n";
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(ViewerJsonContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            NewLine = "\n"
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.MakeReadOnly(populateMissingResolver: false);
        return options;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.Unspecified,
    WriteIndented = true,
    IndentSize = 2,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(SearchFormDefinition))]
[JsonSerializable(typeof(ClinicalSummaryFormDefinition))]
[JsonSerializable(typeof(TilesOptions))]
internal sealed partial class ViewerJsonContext : JsonSerializerContext;
