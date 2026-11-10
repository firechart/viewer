using System.Text.Json;
using System.Text.Json.Nodes;

namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// The components a section of the clinical summary can be shown with, and the reading of their options.
/// </summary>
/// <remarks>
/// A section names its component (<see cref="ClinicalSummaryFormSection.Component"/>) and gives its options as a JSON object
/// (<see cref="ClinicalSummaryFormSection.Options"/>); each component reads them into its own class with the rules of <see cref="ViewerJson"/> (an unknown
/// key is an error). The catalog grows with the components of the Viewer (<c>.tasks/viewer/components.md</c>).
/// </remarks>
/// <example>
/// <code>
/// var section = new ClinicalSummaryFormSection { Code = "problems", Options = new JsonObject { ["showStatus"] = false } };
/// var options = SummaryComponents.TileOptionsOf(form, section);   // ShowStatus false, ShowAge of the form
/// </code>
/// </example>
public static class SummaryComponents
{
    /// <summary>The list of the tiles of the records, newest first: <c>tiles</c>.</summary>
    public const string Tiles = "tiles";

    /// <summary>
    /// The laboratory results: the results under the reports that list them (else by day), a table in a wide section and the tiles in a narrow one:
    /// <c>lab-results</c>. Its options are the ones of the tiles (<see cref="TilesOptions"/>).
    /// </summary>
    public const string LabResults = "lab-results";

    /// <summary>
    /// The trends of measures (vital signs): the measures of the section with their last value, and a chart of the history of the chosen one:
    /// <c>observation-trends</c>. Its options are the ones of the tiles (<see cref="TilesOptions"/>).
    /// </summary>
    public const string ObservationTrends = "observation-trends";

    /// <summary>The components a form may name.</summary>
    public static IReadOnlyList<string> Known { get; } = [Tiles, LabResults, ObservationTrends];

    /// <summary>
    /// Whether a component is one the Viewer has.
    /// </summary>
    /// <param name="component">The name of the component.</param>
    /// <returns><see langword="true"/> for a known component.</returns>
    /// <example>
    /// <code>
    /// SummaryComponents.IsKnown("tiles");   // true
    /// </code>
    /// </example>
    public static bool IsKnown(string? component)
    {
        return component is not null && Known.Contains(component, StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads the options of the tiles of a section.
    /// </summary>
    /// <param name="options">The options of the section, or <see langword="null"/>.</param>
    /// <returns>The options; empty ones for <see langword="null"/>.</returns>
    /// <exception cref="JsonException">The options have a key the tiles do not know or a value of a wrong type.</exception>
    /// <example>
    /// <code>
    /// var tiles = SummaryComponents.ReadTilesOptions(section.Options);
    /// </code>
    /// </example>
    public static TilesOptions ReadTilesOptions(JsonObject? options)
    {
        return options is null ? new TilesOptions() : ViewerJson.Read<TilesOptions>(options.ToJsonString());
    }

    /// <summary>
    /// The options of the rows of a section of the tiles: its own, else the ones of the form; options that cannot be read give the ones of the form.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="section">The section.</param>
    /// <returns>The options of the rows.</returns>
    /// <example>
    /// <code>
    /// var rows = SummaryComponents.TileOptionsOf(form, section);
    /// </code>
    /// </example>
    public static TileOptions TileOptionsOf(ClinicalSummaryFormDefinition form, ClinicalSummaryFormSection section)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(section);
        TilesOptions own;
        try
        {
            own = ReadTilesOptions(section.Options);
        }
        catch (JsonException)
        {
            own = new TilesOptions();
        }

        return new TileOptions(own.ShowAge ?? form.ShowAge, own.ShowStatus ?? form.ShowStatus);
    }
}

/// <summary>
/// The options of a section shown as tiles (<see cref="SummaryComponents.Tiles"/>): each overrides the option of the form for this section.
/// </summary>
/// <example>
/// <code>
/// var options = new TilesOptions { ShowStatus = false };
/// </code>
/// </example>
public sealed class TilesOptions
{
    /// <summary>Whether a row shows the age of the patient at the record; <see langword="null"/> takes <see cref="ClinicalSummaryFormDefinition.ShowAge"/>.</summary>
    public bool? ShowAge { get; set; }

    /// <summary>Whether a row shows its status; <see langword="null"/> takes <see cref="ClinicalSummaryFormDefinition.ShowStatus"/>.</summary>
    public bool? ShowStatus { get; set; }
}
