using Microsoft.AspNetCore.Components;

namespace Flox.Shared.Extensions;

/// <summary>The application shown inside a frame of another one: the pages below <see cref="Prefix"/> are shown without the shell and keep their links below it.</summary>
public static class EmbeddedMode
{
    /// <summary>Path prefix of the embedded pages.</summary>
    public const string Prefix = "/embed";

    /// <summary>Tells whether the current address is below the embedded prefix.</summary>
    public static bool IsEmbedded(this NavigationManager navigationManager)
    {
        return IsEmbeddedPath(navigationManager.ToBaseRelativePath(navigationManager.Uri));
    }

    /// <summary>Tells whether a base-relative path is below the embedded prefix.</summary>
    public static bool IsEmbeddedPath(string baseRelativePath)
    {
        var prefix = Prefix.TrimStart('/');
        return baseRelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
               && (baseRelativePath.Length == prefix.Length || baseRelativePath[prefix.Length] is '/' or '?' or '#');
    }

    /// <summary>Tells whether a route is an embedded one.</summary>
    public static bool IsEmbeddedRoute(string? route)
    {
        return route is not null && route.StartsWith(Prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The route of a page that has an embedded twin, as the current mode needs it.</summary>
    public static string RouteFor(this NavigationManager navigationManager, string route)
    {
        return navigationManager.IsEmbedded() ? Prefix + route : route;
    }
}
