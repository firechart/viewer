using FireChart.Resources;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer;

/// <summary>
/// The name of a summary for the user, by the record it grows from: "Patient Summary", "Encounter Summary", "Summary" for another type.
/// </summary>
/// <example>
/// <code>
/// var title = SummaryCaption.Of(route.ResourceType); // "Encounter Summary"
/// </code>
/// </example>
public static class SummaryCaption
{
    /// <summary>
    /// The name of the summary of a context.
    /// </summary>
    /// <param name="context">The type of the record (<see cref="SummaryContexts"/>); <see langword="null"/> is the patient summary.</param>
    /// <returns>The name in the language of the interface.</returns>
    /// <example>
    /// <code>
    /// SummaryCaption.Of("Patient"); // "Patient Summary"
    /// </code>
    /// </example>
    public static string Of(string? context)
    {
        return context switch
        {
            null or SummaryContexts.Patient => Strings.Caption_PatientSummary,
            SummaryContexts.Encounter => Strings.Caption_EncounterSummary,
            _ => Strings.Caption_Summary
        };
    }
}
