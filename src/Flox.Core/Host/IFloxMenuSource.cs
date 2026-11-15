using System.Security.Claims;

namespace Flox.Core.Host;

/// <summary>Supplies the menu of one module, grouped under its own title; the module owns the order of its items and the host only orders menus against each other.</summary>
public interface IFloxMenuSource
{
    /// <summary>Builds the menu for a user.</summary>
    /// <param name="user">The current user.</param>
    /// <returns>The menu.</returns>
    IFloxMenu GetMenu(ClaimsPrincipal user);
}
