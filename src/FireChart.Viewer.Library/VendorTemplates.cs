using System.Text.Json;
using FireChart.Viewer.Core;

namespace FireChart.Viewer.Library;

/// <summary>
/// The vendor library of templates that ships in this assembly, and the translations of its texts.
/// </summary>
/// <remarks>
/// The templates are the embedded <c>.liquid</c> files of <c>src/FireChart.Viewer.Library/templates</c>; the key is the file name without extension
/// (<c>Patient@tile</c>). The translations are <c>locales/{language}/fhir-templates.json</c> in the format of i18next (the English text and its
/// translation), for the static texts of the templates and the texts of the engine. Put it last among the layers of a <see cref="FhirRenderer"/>:
/// the layers before it override its templates and texts key by key.
/// </remarks>
/// <example>
/// <code>
/// var renderer = new FhirRenderer([customerOverrides, new VendorTemplates()]);
/// </code>
/// </example>
public sealed class VendorTemplates : ITemplateSource, ITemplateTexts
{
    private const string Extension = ".liquid";
    private const string LocalePrefix = "locales.";

    private readonly Dictionary<string, string> _templates = Load();
    private readonly Dictionary<string, Dictionary<string, string>> _texts = LoadTexts();

    /// <inheritdoc/>
    public IEnumerable<string> Keys => _templates.Keys;

    /// <summary>
    /// The languages that have translations (<c>de</c>, <c>es</c>, <c>fr</c>, <c>ru</c>, <c>zh</c>, and <c>en</c> for the plural forms).
    /// </summary>
    public IEnumerable<string> Languages => _texts.Keys;

    /// <inheritdoc/>
    public string? Find(string key)
    {
        return _templates.GetValueOrDefault(key);
    }

    /// <inheritdoc/>
    public string? Translate(string culture, string key)
    {
        return _texts.TryGetValue(culture, out var texts) && texts.TryGetValue(key, out var text) && text.Length > 0 ? text : null;
    }

    private static Dictionary<string, string> Load()
    {
        var assembly = typeof(VendorTemplates).Assembly;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(Extension, StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            result[name[..^Extension.Length]] = reader.ReadToEnd();
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> LoadTexts()
    {
        var assembly = typeof(VendorTemplates).Assembly;
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(LocalePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
            var language = name[LocalePrefix.Length..].Split('\\', '/')[0];
            result[language] = new Dictionary<string, string>(texts, StringComparer.Ordinal);
        }

        return result;
    }
}
