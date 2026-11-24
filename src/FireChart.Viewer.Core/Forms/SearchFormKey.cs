namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// The key of a search form: the resource type of a FHIR release, with a profile for the forms of an implementation guide.
/// </summary>
/// <param name="Release">The FHIR release in lower case (<c>r3</c>, <c>r4</c>, <c>r4b</c>, <c>r5</c>): the form of the library is one for every release,
/// the release chooses the parameters it has.</param>
/// <param name="ResourceType">The resource type (<c>Patient</c>), case-sensitive as in FHIR.</param>
/// <param name="Profile">The canonical URL or the id of a profile (<c>us-core-patient</c>); not used by the library, reserved for the servers that index
/// implementation guides.</param>
/// <example>
/// <code>
/// var key = new SearchFormKey("r4", "Patient");
/// </code>
/// </example>
public sealed record SearchFormKey(string Release, string ResourceType, string? Profile = null);
