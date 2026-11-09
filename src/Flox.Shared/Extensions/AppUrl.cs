namespace Flox.Shared.Extensions;

// The routes of the application are written with a leading slash, like in @page, and stored that way (bookmarks).
// The document's <base> puts the application below the client path of the server (Flox:ClientApp:Path), so a link must be relative
// to the base: a leading slash would lead out of the application. "~/path" is a path of the server itself (the administration)
public static class AppUrl
{
    public const string ServerRootPrefix = "~/";

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

    public static void NavigateToRoute(this NavigationManager navigation, string route, bool forceLoad = false)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        navigation.NavigateTo(ToHref(route), forceLoad);
    }
}
