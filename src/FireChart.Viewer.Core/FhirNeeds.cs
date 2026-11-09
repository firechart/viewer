namespace FireChart.Viewer.Core;

/// <summary>
/// What a template asks the server to send along with the resources of a search.
/// </summary>
/// <remarks>
/// Declared in the template by <c>{% fhir_include "Encounter:subject" %}</c> and <c>{% fhir_revinclude "Observation:encounter" %}</c>; the caller adds them
/// to the search as <c>_include</c> and <c>_revinclude</c>, so the references of the rows resolve without a request per row.
/// </remarks>
/// <param name="Include">The values of <c>_include</c> (<c>Encounter:subject</c>).</param>
/// <param name="RevInclude">The values of <c>_revinclude</c> (<c>Observation:encounter</c>).</param>
/// <example>
/// <code>
/// var needs = renderer.NeedsFor("Encounter", TileModel.Mode);
/// var query = string.Join("&amp;", needs.Include.Select(i =&gt; "_include=" + Uri.EscapeDataString(i)));
/// </code>
/// </example>
public sealed record FhirNeeds(IReadOnlyList<string> Include, IReadOnlyList<string> RevInclude)
{
    /// <summary>
    /// Nothing asked.
    /// </summary>
    public static readonly FhirNeeds None = new([], []);

    /// <summary>
    /// Whether nothing is asked.
    /// </summary>
    public bool IsEmpty => Include.Count == 0 && RevInclude.Count == 0;
}
