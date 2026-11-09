using System.Text.RegularExpressions;

namespace FireChart.Viewer.Routing;

/// <summary>
/// The pages of the Viewer.
/// </summary>
public enum ViewerPage
{
    /// <summary>The resource types of the server (<c>resources</c>; the empty address too).</summary>
    ResourceTypes,

    /// <summary>The list of the resources of a type (<c>resources/Patient</c>).</summary>
    ResourceList,

    /// <summary>One resource (<c>resources/Patient/123</c>).</summary>
    Resource,

    /// <summary>The clinical summary, of a patient or the choice of one (<c>summary</c>, <c>summary/123</c>).</summary>
    ClinicalSummary
}

/// <summary>
/// An address of the Viewer, below the prefix of its host. The routing of the Viewer is experimental.
/// </summary>
/// <param name="Page">The page.</param>
/// <param name="ResourceType">The resource type of a list or a resource.</param>
/// <param name="Id">The id of a resource, or of the patient of a summary.</param>
/// <param name="Query">The query without the question mark (a FHIR search of a list); empty for none.</param>
/// <example>
/// <code>
/// var route = new ViewerRoute(ViewerPage.Resource, "Patient", "123");
/// session.Navigate(route); // .../resources/Patient/123
/// </code>
/// </example>
public sealed record ViewerRoute(ViewerPage Page, string? ResourceType = null, string? Id = null, string Query = "")
{
    /// <summary>
    /// The address of the route, relative to the prefix of the host (<c>resources/Patient?family=Smith</c>).
    /// </summary>
    public string Location
    {
        get
        {
            var path = Page switch
            {
                ViewerPage.ResourceTypes => ViewerRoutes.Resources,
                ViewerPage.ResourceList => $"{ViewerRoutes.Resources}/{Uri.EscapeDataString(ResourceType ?? string.Empty)}",
                ViewerPage.Resource => $"{ViewerRoutes.Resources}/{Uri.EscapeDataString(ResourceType ?? string.Empty)}/{Uri.EscapeDataString(Id ?? string.Empty)}",
                _ => Id is null ? ViewerRoutes.Summary : $"{ViewerRoutes.Summary}/{Uri.EscapeDataString(Id)}"
            };
            return string.IsNullOrEmpty(Query) ? path : $"{path}?{Query}";
        }
    }
}

/// <summary>
/// The table of the addresses of the Viewer: an address below the prefix of the host is one of its pages, or none. The routing of the Viewer is
/// experimental.
/// </summary>
/// <remarks>
/// Only the addresses of the Viewer are accepted: no scheme, no other host, no <c>//</c>, <c>\</c>, <c>..</c> or encoded dot, letters only in a resource
/// type, the characters of a FHIR id in an id.
/// </remarks>
/// <example>
/// <code>
/// if (ViewerRoutes.Parse("resources/Patient?family=Smith") is { Page: ViewerPage.ResourceList } list)
/// {
///     Console.WriteLine(list.ResourceType); // Patient
/// }
/// </code>
/// </example>
public static partial class ViewerRoutes
{
    /// <summary>The first segment of the pages of the resources: <c>resources</c>.</summary>
    public const string Resources = "resources";

    /// <summary>The first segment of the clinical summary: <c>summary</c>.</summary>
    public const string Summary = "summary";

    [GeneratedRegex("^[A-Za-z]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ResourceTypePattern();

    [GeneratedRegex("^[A-Za-z0-9.-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>
    /// The page of an address.
    /// </summary>
    /// <param name="location">The address relative to the prefix of the host, with its query (<c>resources/Patient/123</c>); empty is the resource types.</param>
    /// <returns>The route, or <see langword="null"/> when the address is not one of the Viewer.</returns>
    public static ViewerRoute? Parse(string? location)
    {
        location ??= string.Empty;
        if (!IsSafe(location))
        {
            return null;
        }

        var question = location.IndexOf('?', StringComparison.Ordinal);
        var path = (question < 0 ? location : location[..question]).Trim('/');
        var query = question < 0 ? string.Empty : location[(question + 1)..];
        string[] segments = path.Length == 0 ? [] : [.. path.Split('/').Select(Uri.UnescapeDataString)];
        return segments switch
        {
            [] or [Resources] => new ViewerRoute(ViewerPage.ResourceTypes, Query: query),
            [Resources, var type] when ResourceTypePattern().IsMatch(type) => new ViewerRoute(ViewerPage.ResourceList, type, Query: query),
            [Resources, var type, var id] when ResourceTypePattern().IsMatch(type) && IdPattern().IsMatch(id) => new ViewerRoute(ViewerPage.Resource, type, id, query),
            [Summary] => new ViewerRoute(ViewerPage.ClinicalSummary, Query: query),
            [Summary, var patient] when IdPattern().IsMatch(patient) => new ViewerRoute(ViewerPage.ClinicalSummary, "Patient", patient, query),
            _ => null
        };
    }

    /// <summary>
    /// The address may be one of the Viewer: relative, without a way to leave the prefix of the host.
    /// </summary>
    /// <param name="location">The address relative to the prefix.</param>
    /// <returns><see langword="false"/> for a scheme, a host, <c>//</c>, <c>\</c>, <c>..</c> or an encoded dot or slash.</returns>
    public static bool IsSafe(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        var path = location.Split('?', 2)[0];
        return !path.StartsWith('/')
               && !path.Contains("//", StringComparison.Ordinal)
               && !path.Contains('\\', StringComparison.Ordinal)
               && !path.Contains(':', StringComparison.Ordinal)
               && !path.Split('/').Contains("..")
               && !path.Contains("%2e", StringComparison.OrdinalIgnoreCase)
               && !path.Contains("%2f", StringComparison.OrdinalIgnoreCase)
               && !path.Contains("%5c", StringComparison.OrdinalIgnoreCase);
    }
}
