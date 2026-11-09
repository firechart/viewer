using Microsoft.AspNetCore.Components;

namespace Flox.Shared.Extensions;

// The application inside a frame of another one: the pages below /embed are shown without the shell and keep their links below /embed
public static class EmbeddedMode
{
    public const string Prefix = "/embed";

    public static bool IsEmbedded(this NavigationManager navigationManager)
    {
        return IsEmbeddedPath(navigationManager.ToBaseRelativePath(navigationManager.Uri));
    }

    public static bool IsEmbeddedPath(string baseRelativePath)
    {
        var prefix = Prefix.TrimStart('/');
        return baseRelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
               && (baseRelativePath.Length == prefix.Length || baseRelativePath[prefix.Length] is '/' or '?' or '#');
    }

    public static bool IsEmbeddedRoute(string? route)
    {
        return route is not null && route.StartsWith(Prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    // The route of a page that has an embedded twin, as the current mode needs it
    public static string RouteFor(this NavigationManager navigationManager, string route)
    {
        return navigationManager.IsEmbedded() ? Prefix + route : route;
    }
}
