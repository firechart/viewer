namespace FireChart.Viewer.Core;

// The texts of the engine itself: keys of the locales of the library (locales/{language}/fhir-templates.json), translated and overridden like the texts of the templates
internal static class EngineTexts
{
    public const string Yes = "Yes";
    public const string No = "No";

    // The age of a patient: whole years, else months, else days (the filters age and age_at)
    public const string Years = "{{count}} y";
    public const string Months = "{{count}} mo";
    public const string Days = "{{count}} d";

    public static IReadOnlyList<string> Keys { get; } = [Yes, No, Years, Months, Days];
}
