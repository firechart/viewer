using System.Reflection;

namespace FireChart.Viewer.Library;

/// <summary>
/// The layout of the vendor library: the static files of <c>library</c> (a package of the designer with its manifest
/// <c>firechart.project.json</c>; no code project carries them), served by every host as <c>library/...</c>, and read by a client one by one when
/// they are needed.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>components/{Name}.xml</c>: the components that show the records (<c>Patient.tile</c>, <c>Patient.summary</c>): a name says
/// where its file is, there is no index; the search form of a type is the component <c>{Type}.search</c>.</description></item>
/// <item><description><c>locales/strings.resx</c> and <c>strings.{culture}.resx</c>: the texts of the components.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var url = VendorLibrary.UrlOf("components/Patient.tile.xml");   // library/components/Patient.tile.xml?v=1.2.0
/// </code>
/// </example>
public static class VendorLibrary
{
    /// <summary>
    /// Where a host serves the static files of this library, relative to its base.
    /// </summary>
    public const string ContentRoot = "library";

    /// <summary>
    /// The version of the library, the query of <see cref="UrlOf"/>: a new release reads new files, not the ones the browser cached.
    /// </summary>
    public static string Version { get; } = typeof(VendorLibrary).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";

    /// <summary>
    /// The address of a file of the library for a client: below <see cref="ContentRoot"/>, with the version as the query.
    /// </summary>
    /// <param name="path">The path of the file in the library (<c>components/Patient.tile.xml</c>).</param>
    /// <returns>The address relative to the base of the host.</returns>
    /// <example>
    /// <code>
    /// var url = VendorLibrary.UrlOf("components/Patient.search.xml");
    /// </code>
    /// </example>
    public static string UrlOf(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return $"{ContentRoot}/{path}?v={Uri.EscapeDataString(Version)}";
    }
}
