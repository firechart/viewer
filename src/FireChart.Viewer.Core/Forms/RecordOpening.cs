namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// Where a record of a section of the patient summary opens (<see cref="SummaryFormSection.OpenIn"/>): in a sheet over the summary, or on its
/// own page.
/// </summary>
/// <remarks>
/// Without a choice of the section a record opens in a sheet, except a patient and an encounter: the records of others are listed on their pages.
/// </remarks>
/// <example>
/// <code>
/// RecordOpening.Resolve(null, "Observation");   // "sheet"
/// RecordOpening.Resolve(null, "Encounter");     // "page"
/// RecordOpening.Resolve("page", "Observation"); // "page"
/// </code>
/// </example>
public static class RecordOpening
{
    /// <summary>In a sheet over the summary: from the bottom on a narrow screen, from the right on a wide one; <c>sheet</c>.</summary>
    public const string Sheet = "sheet";

    /// <summary>On the page of the record: <c>page</c>.</summary>
    public const string Page = "page";

    /// <summary>The values a form may name.</summary>
    public static IReadOnlyList<string> Known { get; } = [Sheet, Page];

    // The records others are listed under: their page is the place to look at them
    private static readonly HashSet<string> s_pageTypes = new(StringComparer.Ordinal) { "Patient", "Encounter" };

    /// <summary>
    /// Where a record opens.
    /// </summary>
    /// <param name="openIn">The choice of the section; <see langword="null"/>, empty or unknown for none.</param>
    /// <param name="resourceType">The type of the record.</param>
    /// <returns><see cref="Sheet"/> or <see cref="Page"/>.</returns>
    /// <example>
    /// <code>
    /// var sheet = RecordOpening.Resolve(section.OpenIn, item.ResourceType) == RecordOpening.Sheet;
    /// </code>
    /// </example>
    public static string Resolve(string? openIn, string resourceType)
    {
        return openIn?.Trim() switch
        {
            Sheet => Sheet,
            Page => Page,
            _ => s_pageTypes.Contains(resourceType) ? Page : Sheet
        };
    }
}
