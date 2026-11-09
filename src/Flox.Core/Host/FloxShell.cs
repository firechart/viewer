using System.Reflection;
using System.Security.Claims;

namespace Flox.Core.Host;

public sealed class FloxShell : IFloxShell
{
    public FloxShell(IEnumerable<IFloxMenuSource> menuSources, IBootstrapper bootstrapper)
    {
        _bootstraper = bootstrapper;
        _menuSources.AddRange(menuSources);
    }

    private readonly IBootstrapper _bootstraper;
    private readonly List<IFloxMenuSource> _menuSources = [];

    // One menu per module, ordered by Position against each other. A menu's own Items are never
    // reordered or merged here - the module supplies them already in the order it wants rendered.
    public IReadOnlyList<IFloxMenu> GetMenus(ClaimsPrincipal user)
    {
        return _menuSources.Select(source => source.GetMenu(user))
                           .OrderBy(menu => menu.Position)
                           .ToList();
    }

    public IReadOnlyList<Assembly> BlazorAssemblies
    {
        get
        {
            return _bootstraper.FloxAssemblies;
        }
    }
}
