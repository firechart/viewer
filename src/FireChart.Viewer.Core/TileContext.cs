namespace FireChart.Viewer.Core;

/// <summary>
/// How the rows of a list are drawn: what the page already shows above them, so a row leaves it out, and the options of the rows.
/// </summary>
/// <param name="Shown">The references (<c>Patient/p1</c>) the page shows above the list (the banner of the patient): a row does not repeat them.</param>
/// <param name="Options">The options of the rows.</param>
/// <param name="Format">How the values of the rows are shown (<c>fhir_options.decimals</c> of the templates); <see langword="null"/> as the records have them.</param>
/// <example>
/// <code>
/// var context = new TileContext(["Patient/p1"], new TileOptions(ShowAge: true));
/// var row = await renderer.RenderTileAsync(observationJson, context: context);
/// </code>
/// </example>
public sealed record TileContext(IReadOnlyList<string> Shown, TileOptions Options, Forms.ValueFormat? Format = null)
{
    /// <summary>
    /// Nothing shown above the list, the default options.
    /// </summary>
    public static TileContext None { get; } = new([], TileOptions.Default);
}

/// <summary>
/// The options of the rows of a list.
/// </summary>
/// <param name="ShowAge">Whether a row shows the age of the patient at the moment of the record.</param>
/// <param name="ShowStatus">Whether a row shows the badge of the status of the record.</param>
/// <example>
/// <code>
/// var options = new TileOptions(ShowAge: true, ShowStatus: false);
/// </code>
/// </example>
public sealed record TileOptions(bool ShowAge = false, bool ShowStatus = true)
{
    /// <summary>
    /// No age, the status shown.
    /// </summary>
    public static TileOptions Default { get; } = new();
}
