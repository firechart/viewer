namespace FireChart.Viewer.Core;

/// <summary>
/// The options of the rows of a list.
/// </summary>
/// <param name="ShowAge">Whether a row shows the age of the patient at the moment of the record.</param>
/// <param name="ShowStatus">Whether a row shows the badge of the status of the record.</param>
/// <param name="CodeSystems">
/// The code systems whose code a row shows before its title, in the order of preference (<c>http://hl7.org/fhir/sid/icd-10</c>): the code of the first
/// system of the list the record has; <see langword="null"/> or empty for none.
/// </param>
/// <param name="Decimals">The most decimal places of a number a row shows (the value of an observation); <see langword="null"/> for the digits of the
/// record.</param>
/// <example>
/// <code>
/// var options = new TileOptions(ShowAge: true, ShowStatus: false, CodeSystems: ["http://hl7.org/fhir/sid/icd-10"]);
/// </code>
/// </example>
public sealed record TileOptions(bool ShowAge = false, bool ShowStatus = true, IReadOnlyList<string>? CodeSystems = null, int? Decimals = null)
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
        return other is not null && ShowAge == other.ShowAge && ShowStatus == other.ShowStatus && Decimals == other.Decimals
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
        return HashCode.Combine(ShowAge, ShowStatus, CodeSystems?.Count ?? 0, Decimals);
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
