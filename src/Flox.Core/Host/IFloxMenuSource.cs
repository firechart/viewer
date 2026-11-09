using System.Security.Claims;

namespace Flox.Core.Host;

// A module exports exactly one menu (grouped under its own title in NavMenu); the module owns
// its Items' order (see IFloxMenu.Items), the host only orders menus against each other.
public interface IFloxMenuSource
{
    IFloxMenu GetMenu(ClaimsPrincipal user);
}
