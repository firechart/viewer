using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Library;

/// <summary>
/// The summary forms of the vendor library: the embedded <c>src/FireChart.Viewer.Library/summary-forms/{code}.json</c>.
/// </summary>
/// <remarks>
/// Every call of <see cref="Find"/> reads a new instance, so a caller may change what it gets. The form <see cref="ClinicalSummaryFormDefinition.DefaultCode"/>
/// is always there; it is the sections of the International Patient Summary (IPS) and the encounters of the patient.
/// </remarks>
/// <example>
/// <code>
/// var form = VendorSummaryForms.Find(server.ClinicalSummaryFormCode ?? ClinicalSummaryFormDefinition.DefaultCode) ?? VendorSummaryForms.Default;
/// </code>
/// </example>
public static class VendorSummaryForms
{
    private const string Prefix = "summary-forms.";
    private const string Extension = ".json";

    private static readonly Dictionary<string, string> s_texts = Load();

    /// <summary>
    /// The codes of the vendor forms, in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> Codes { get; } = [.. s_texts.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// The default form, a new instance.
    /// </summary>
    public static ClinicalSummaryFormDefinition Default => Find(ClinicalSummaryFormDefinition.DefaultCode)
                                                           ?? throw new InvalidOperationException("The vendor library has no default summary form.");

    /// <summary>
    /// Finds a vendor form.
    /// </summary>
    /// <param name="code">The code of the form (<c>default</c>).</param>
    /// <returns>A new instance of the form, or <see langword="null"/> when the library has no form with this code.</returns>
    public static ClinicalSummaryFormDefinition? Find(string code)
    {
        return s_texts.TryGetValue(code, out var text) ? ViewerJson.Read<ClinicalSummaryFormDefinition>(text) : null;
    }

    /// <summary>
    /// The text of a vendor form as it is stored.
    /// </summary>
    /// <param name="code">The code of the form.</param>
    /// <returns>The JSON text, or <see langword="null"/> when the library has no form with this code.</returns>
    public static string? TextOf(string code)
    {
        return s_texts.GetValueOrDefault(code);
    }

    private static Dictionary<string, string> Load()
    {
        var assembly = typeof(VendorSummaryForms).Assembly;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(Extension, StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            result[name[Prefix.Length..^Extension.Length]] = reader.ReadToEnd();
        }

        return result;
    }
}
