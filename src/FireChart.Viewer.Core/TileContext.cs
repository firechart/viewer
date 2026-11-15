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
/// <param name="CodeSystems">
/// The code systems whose code a row shows before its title, in the order of preference (<c>http://hl7.org/fhir/sid/icd-10</c>): the code of the first
/// system of the list the record has (<see cref="TileCode"/>); <see langword="null"/> or empty for none.
/// </param>
/// <example>
/// <code>
/// var options = new TileOptions(ShowAge: true, ShowStatus: false, CodeSystems: ["http://hl7.org/fhir/sid/icd-10"]);
/// </code>
/// </example>
public sealed record TileOptions(bool ShowAge = false, bool ShowStatus = true, IReadOnlyList<string>? CodeSystems = null)
{
    /// <summary>
    /// No age, the status shown, no code.
    /// </summary>
    public static TileOptions Default { get; } = new();

    /// <summary>
    /// Whether two options are the same; the code systems are compared by their items.
    /// </summary>
    /// <param name="other">The other options.</param>
    /// <returns><see langword="true"/> when they lay a row out the same way.</returns>
    /// <example>
    /// <code>
    /// new TileOptions(CodeSystems: ["http://hl7.org/fhir/sid/icd-10"]) == new TileOptions(CodeSystems: ["http://hl7.org/fhir/sid/icd-10"]);   // true
    /// </code>
    /// </example>
    public bool Equals(TileOptions? other)
    {
        return other is not null && ShowAge == other.ShowAge && ShowStatus == other.ShowStatus
               && (CodeSystems ?? []).SequenceEqual(other.CodeSystems ?? [], StringComparer.Ordinal);
    }

    /// <summary>
    /// The hash of the options, equal for equal options.
    /// </summary>
    /// <returns>The hash.</returns>
    /// <example>
    /// <code>
    /// var hash = TileOptions.Default.GetHashCode();
    /// </code>
    /// </example>
    public override int GetHashCode()
    {
        return HashCode.Combine(ShowAge, ShowStatus, CodeSystems?.Count ?? 0);
    }
}

/// <summary>
/// A code of a record a row may show before its title (<see cref="TileOptions.CodeSystems"/>): a coding of its main code, named by the template with
/// <c>{% slot_data codes: self.code %}</c>.
/// </summary>
/// <param name="System">The URI of the code system (<c>http://hl7.org/fhir/sid/icd-10</c>).</param>
/// <param name="Code">The code (<c>E11.9</c>).</param>
/// <example>
/// <code>
/// var code = new TileCode("http://hl7.org/fhir/sid/icd-10", "E11.9");
/// </code>
/// </example>
public sealed record TileCode(string System, string Code);
