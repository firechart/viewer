namespace FireChart.Viewer.Core;

// The texts of the engine itself: keys of the locales of the library (locales/{language}/fhir-templates.json), translated and overridden like the texts of the templates
internal static class EngineTexts
{
    public const string Yes = "Yes";
    public const string No = "No";

    public static IReadOnlyList<string> Keys { get; } = [Yes, No];
}
