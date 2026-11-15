namespace Flox.Shared.Extensions;

/// <summary>
/// Builds links of the application from its routes. A route is written with a leading slash, as in <c>@page</c>, and stored that way (bookmarks).
/// The document base puts the application below the client path of the server, so a link must be relative to the base; a route that starts with
/// <see cref="ServerRootPrefix"/> is a path of the server itself (the administration).
/// </summary>
public static class AppUrl
{
    /// <summary>Prefix of a route that is a path of the server itself, not of the client application.</summary>
    public const string ServerRootPrefix = "~/";

    /// <summary>Turns a route into a link relative to the document base; a server route loses its prefix.</summary>
    public static string ToHref(string? route)
    {
        if (string.IsNullOrEmpty(route))
        {
            return string.Empty;
        }

        if (route.StartsWith(ServerRootPrefix, StringComparison.Ordinal))
        {
            return route[1..];
        }

        if (route[0] != '/' || route.StartsWith("//", StringComparison.Ordinal))
        {
            return route;
        }

        var rest = route[1..];
        return rest.Length == 0 || rest[0] is '?' or '#' ? "./" + rest : rest;
    }

    /// <summary>Navigates to a route of the application, resolving it against the document base.</summary>
    public static void NavigateToRoute(this NavigationManager navigation, string route, bool forceLoad = false)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        navigation.NavigateTo(ToHref(route), forceLoad);
    }
}
