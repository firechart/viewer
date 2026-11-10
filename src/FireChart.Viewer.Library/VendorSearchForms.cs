using System.Text.RegularExpressions;

namespace FireChart.Viewer.Library;

/// <summary>
/// Where the search forms of the vendor library are: the static files <c>wwwroot/search-forms/{release}/{type}.json</c> of this library, one form per
/// resource type of a FHIR release (<c>r3</c>, <c>r4</c>, <c>r4b</c>, <c>r5</c>), served by every host as
/// <c>_content/FireChart.Viewer.Library/search-forms/{release}/{type}.json</c>.
/// </summary>
/// <remarks>
/// The forms are not in the assembly: a client reads the one it needs when it needs it (<see cref="VendorSearchFormProvider"/> through
/// <see cref="FireChart.Viewer.Core.IViewerAssets"/>), a server reads the file from its web root. A form with
/// <see cref="FireChart.Viewer.Core.Forms.SearchFormDefinition.IsGenerated"/> was made from the search parameters of the core package and not designed
/// yet: a designed form of any other layer is preferred to it.
/// </remarks>
/// <example>
/// <code>
/// if (VendorSearchForms.UrlOf("r4", "Patient") is { } url)
/// {
///     var text = await assets.ReadTextAsync(url, cancellationToken);
/// }
/// </code>
/// </example>
public static partial class VendorSearchForms
{
    /// <summary>
    /// The folder of the forms in the <c>wwwroot</c> of the library.
    /// </summary>
    public const string Folder = "search-forms";

    /// <summary>
    /// The folder of the static files of a project.
    /// </summary>
    public const string WebRoot = VendorLibrary.WebRoot;

    /// <summary>
    /// Where a host serves the static files of this library, relative to its base.
    /// </summary>
    public const string ContentRoot = VendorLibrary.ContentRoot;

    /// <summary>
    /// The version of the library, the query of <see cref="UrlOf"/>: a new release reads new files, not the ones the browser cached.
    /// </summary>
    public static string Version => VendorLibrary.Version;

    /// <summary>
    /// The path of the form of a resource type, relative to the base of the host.
    /// </summary>
    /// <param name="release">The release (<c>r4</c>); the case does not matter.</param>
    /// <param name="resourceType">The resource type (<c>Patient</c>), case-sensitive as in FHIR.</param>
    /// <returns>The path without a query, or <see langword="null"/> when the release or the type is not a plain name (nothing to read).</returns>
    /// <example>
    /// <code>
    /// var path = VendorSearchForms.PathOf("R4", "Patient");   // _content/FireChart.Viewer.Library/search-forms/r4/Patient.json
    /// </code>
    /// </example>
    public static string? PathOf(string release, string resourceType)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(resourceType);
        return NameRegex().IsMatch(release) && NameRegex().IsMatch(resourceType) ? $"{ContentRoot}/{Folder}/{release.ToLowerInvariant()}/{resourceType}.json" : null;
    }

    /// <summary>
    /// The address of the form of a resource type for a client: <see cref="PathOf"/> with the version of the library as the query.
    /// </summary>
    /// <param name="release">The release (<c>r4</c>); the case does not matter.</param>
    /// <param name="resourceType">The resource type (<c>Patient</c>), case-sensitive as in FHIR.</param>
    /// <returns>The address, or <see langword="null"/> when the release or the type is not a plain name.</returns>
    /// <example>
    /// <code>
    /// var url = VendorSearchForms.UrlOf("r4", "Patient");   // _content/FireChart.Viewer.Library/search-forms/r4/Patient.json?v=1.2.0
    /// </code>
    /// </example>
    public static string? UrlOf(string release, string resourceType)
    {
        return PathOf(release, resourceType) is { } path ? $"{path}?v={Uri.EscapeDataString(Version)}" : null;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{0,63}$")]
    private static partial Regex NameRegex();
}
