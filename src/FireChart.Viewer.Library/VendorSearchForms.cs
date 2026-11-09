using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Library;

/// <summary>
/// The search forms of the vendor library: the embedded <c>src/FireChart.Viewer.Library/search-forms/{release}/{type}.json</c>, one form per resource type of
/// a FHIR release (<c>r3</c>, <c>r4</c>, <c>r4b</c>, <c>r5</c>).
/// </summary>
/// <remarks>
/// Every call of <see cref="Find"/> reads a new instance, so a caller may change what it gets. A form with <see cref="SearchFormDefinition.IsGenerated"/>
/// was made from the search parameters of the core package and not designed yet: a designed form of any other layer is preferred to it.
/// </remarks>
/// <example>
/// <code>
/// var form = VendorSearchForms.Find("r4", "Patient");
/// if (form is not null)
/// {
///     Console.WriteLine($"{form.Name}: {form.Fields.Count(field => field.Included)} fields");
/// }
/// </code>
/// </example>
public static class VendorSearchForms
{
    private const string Prefix = "search-forms.";
    private const string Extension = ".json";

    private static readonly Dictionary<string, Dictionary<string, string>> s_texts = Load();

    /// <summary>
    /// The releases that have forms, lower case, in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> Releases { get; } = [.. s_texts.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// The resource types of a release that have a form.
    /// </summary>
    /// <param name="release">The release, as in the folder names (<c>r4</c>); the case does not matter.</param>
    /// <returns>The resource types in ordinal order; empty for a release without forms.</returns>
    /// <example>
    /// <code>
    /// foreach (var type in VendorSearchForms.ResourceTypes("r5"))
    /// {
    ///     Console.WriteLine(type);
    /// }
    /// </code>
    /// </example>
    public static IReadOnlyList<string> ResourceTypes(string release)
    {
        return s_texts.TryGetValue(release.ToLowerInvariant(), out var forms) ? [.. forms.Keys.Order(StringComparer.Ordinal)] : [];
    }

    /// <summary>
    /// Finds the vendor form of a resource type.
    /// </summary>
    /// <param name="release">The release (<c>r4</c>); the case does not matter.</param>
    /// <param name="resourceType">The resource type (<c>Patient</c>), case-sensitive as in FHIR.</param>
    /// <returns>A new instance of the form, or <see langword="null"/> when the library has no form for the type.</returns>
    /// <exception cref="System.Text.Json.JsonException">The embedded file is not a valid form (the CI tests of the library prevent it).</exception>
    /// <example>
    /// <code>
    /// var form = VendorSearchForms.Find("r4", "Observation") ?? throw new InvalidOperationException("no vendor form");
    /// </code>
    /// </example>
    public static SearchFormDefinition? Find(string release, string resourceType)
    {
        return TextOf(release, resourceType) is { } text ? ViewerJson.Read<SearchFormDefinition>(text) : null;
    }

    /// <summary>
    /// The text of a vendor form as it is in the file.
    /// </summary>
    /// <param name="release">The release (<c>r4</c>); the case does not matter.</param>
    /// <param name="resourceType">The resource type (<c>Patient</c>).</param>
    /// <returns>The JSON of the form, or <see langword="null"/> when the library has no form for the type.</returns>
    /// <example>
    /// <code>
    /// var version = VendorSearchForms.TextOf("r4", "Patient") is { } text ? TemplateHash.Of(text) : null;
    /// </code>
    /// </example>
    public static string? TextOf(string release, string resourceType)
    {
        return s_texts.TryGetValue(release.ToLowerInvariant(), out var forms) ? forms.GetValueOrDefault(resourceType) : null;
    }

    // search-forms.r4\Patient.json (the folder separator is the one of the machine that built the assembly)
    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        var assembly = typeof(VendorSearchForms).Assembly;
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(Extension, StringComparison.Ordinal)))
        {
            var key = name[Prefix.Length..^Extension.Length].Replace('\\', '/');
            var slash = key.IndexOf('/', StringComparison.Ordinal);
            if (slash <= 0)
            {
                continue;
            }

            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            var release = key[..slash].ToLowerInvariant();
            if (!result.TryGetValue(release, out var forms))
            {
                result[release] = forms = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            forms[key[(slash + 1)..]] = reader.ReadToEnd();
        }

        return result;
    }
}
