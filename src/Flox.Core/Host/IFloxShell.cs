using System.Reflection;
using System.Security.Claims;

namespace Flox.Core.Host;

public interface IFloxShell
{
    IReadOnlyList<Assembly> BlazorAssemblies { get; }
    IReadOnlyList<IFloxMenu> GetMenus(ClaimsPrincipal user);
}
