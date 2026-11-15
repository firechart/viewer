using System.Reflection;
using System.Security.Claims;

namespace Flox.Core.Host;

/// <summary>The shell of the client application: its assemblies with pages and its menus.</summary>
public interface IFloxShell
{
    /// <summary>The assemblies that hold Blazor pages and components of Flox.</summary>
    IReadOnlyList<Assembly> BlazorAssemblies { get; }
    /// <summary>Builds the menus a user may see.</summary>
    /// <param name="user">The current user.</param>
    /// <returns>The menus in display order.</returns>
    IReadOnlyList<IFloxMenu> GetMenus(ClaimsPrincipal user);
}
