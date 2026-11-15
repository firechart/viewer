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

    /// <summary>
    /// The summary of a record and of what refers to it: of a patient (<c>summary/Patient/123</c>; the older <c>summary/123</c> too), or the choice of a
    /// patient (<c>summary</c>). The <see cref="ViewerRoute.ResourceType"/> of the route is the type of the record the summary grows from.
    /// </summary>
    Summary,

    /// <summary>The bookmarks of the Viewer on this device (<c>bookmarks</c>), of every server.</summary>
    Bookmarks
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
                ViewerPage.Bookmarks => ViewerRoutes.Bookmarks,
                ViewerPage.ResourceList => $"{ViewerRoutes.Resources}/{Uri.EscapeDataString(ResourceType ?? string.Empty)}",
                ViewerPage.Resource => $"{ViewerRoutes.Resources}/{Uri.EscapeDataString(ResourceType ?? string.Empty)}/{Uri.EscapeDataString(Id ?? string.Empty)}",
                _ => Id is null ? ViewerRoutes.Summary : $"{ViewerRoutes.Summary}/{Uri.EscapeDataString(ResourceType ?? ViewerRoutes.PatientContext)}/{Uri.EscapeDataString(Id)}"
            };
            return string.IsNullOrEmpty(Query) ? path : $"{path}?{Query}";
        }
    }

    /// <summary>
    /// The record shown in a sheet over the patient summary (<c>summary/Patient/123?peek=Observation/o1</c>), as the route of its page;
    /// <see langword="null"/> for no sheet, a value that is not a record, or another page.
    /// </summary>
    public ViewerRoute? Peek => Page == ViewerPage.Summary && Id is not null ? ViewerRoutes.PeekOf(Query) : null;

    /// <summary>
    /// The same page with a record in a sheet over it, or without the sheet.
    /// </summary>
    /// <param name="record">The page of the record (<see cref="ViewerPage.Resource"/>); <see langword="null"/> closes the sheet.</param>
    /// <returns>The route; the rest of its query is kept.</returns>
    /// <example>
    /// <code>
    /// var sheet = summary.WithPeek(new ViewerRoute(ViewerPage.Resource, "Observation", "o1")); // summary/Patient/123?peek=Observation/o1
    /// var closed = sheet.WithPeek(null);                                                      // summary/Patient/123
    /// </code>
    /// </example>
    public ViewerRoute WithPeek(ViewerRoute? record)
    {
        var rest = Query.Split('&', StringSplitOptions.RemoveEmptyEntries).Where(part => !part.StartsWith(ViewerRoutes.PeekParameter + "=", StringComparison.Ordinal));
        string[] peek = record is { ResourceType: { } type, Id: { } id } ? [$"{ViewerRoutes.PeekParameter}={Uri.EscapeDataString(type)}/{Uri.EscapeDataString(id)}"] : [];
        return this with { Query = string.Join('&', rest.Concat(peek)) };
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

    /// <summary>The first segment of a summary: <c>summary</c>.</summary>
    public const string Summary = "summary";

    /// <summary>The type of the record of the patient summary: <c>Patient</c> (<c>summary/Patient/123</c>).</summary>
    public const string PatientContext = "Patient";

    /// <summary>The page of the bookmarks: <c>bookmarks</c>.</summary>
    public const string Bookmarks = "bookmarks";

    /// <summary>The parameter of the query of the patient summary that holds the record of the sheet over it (<c>peek=Observation/o1</c>).</summary>
    public const string PeekParameter = "peek";

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
            // The list of the patients that led to the patient summary is the list of the patients now
            [Summary] => new ViewerRoute(ViewerPage.ResourceList, PatientContext, Query: query),
            [Bookmarks] => new ViewerRoute(ViewerPage.Bookmarks),
            [Summary, var type, var id] when ResourceTypePattern().IsMatch(type) && IdPattern().IsMatch(id) => new ViewerRoute(ViewerPage.Summary, type, id, query),
            [Summary, var patient] when IdPattern().IsMatch(patient) => new ViewerRoute(ViewerPage.Summary, PatientContext, patient, query),
            _ => null
        };
    }

    // The page of the record of peek=Type/id in a query; null when there is none or it is not a record
    internal static ViewerRoute? PeekOf(string query)
    {
        var value = query.Split('&').FirstOrDefault(part => part.StartsWith(PeekParameter + "=", StringComparison.Ordinal))?[(PeekParameter.Length + 1)..];
        string[] parts = value is null ? [] : Uri.UnescapeDataString(value).Split('/');
        return parts is [var type, var id] && ResourceTypePattern().IsMatch(type) && IdPattern().IsMatch(id) ? new ViewerRoute(ViewerPage.Resource, type, id) : null;
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
