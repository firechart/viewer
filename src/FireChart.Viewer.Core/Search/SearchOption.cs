namespace FireChart.Viewer.Core.Search;

/// <summary>
/// An option of a field with a list of codes.
/// </summary>
/// <param name="Code">The code.</param>
/// <param name="Display">The text for people; the code when there is none.</param>
/// <param name="System">The code system, or <see langword="null"/>.</param>
/// <example>
/// <code>
/// var female = new SearchOption("female", "Female", "http://hl7.org/fhir/administrative-gender");
/// var token = female.Key; // http://hl7.org/fhir/administrative-gender|female
/// </code>
/// </example>
public sealed record SearchOption
(
    string Code,
    string Display,
    string? System = null
)
{
    /// <summary>
    /// The token as the server wants it: <c>system|code</c>, or <c>code</c> without a system; escaped.
    /// </summary>
    public string Key
    {
        get
        {
            return System is null ? SearchFhirFormat.Escape(Code) : $"{SearchFhirFormat.Escape(System)}|{SearchFhirFormat.Escape(Code)}";
        }
    }

    /// <summary>
    /// The text of the option in a list: <c>Display (code)</c>, or the code alone.
    /// </summary>
    public string Caption
    {
        get
        {
            return string.IsNullOrWhiteSpace(Display) || Display == Code ? Code : $"{Display} ({Code})";
        }
    }
}
