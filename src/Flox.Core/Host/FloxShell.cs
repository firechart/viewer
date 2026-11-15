using System.Reflection;
using System.Security.Claims;

namespace Flox.Core.Host;

/// <summary>The default <see cref="IFloxShell"/>: collects the menus of the registered menu sources.</summary>
public sealed class FloxShell : IFloxShell
{
    /// <summary>Creates the shell.</summary>
    /// <param name="menuSources">The sources of the menus.</param>
    /// <param name="bootstrapper">The bootstrapper that knows the Flox assemblies.</param>
    public FloxShell(IEnumerable<IFloxMenuSource> menuSources, IBootstrapper bootstrapper)
    {
        _bootstraper = bootstrapper;
        _menuSources.AddRange(menuSources);
    }

    private readonly IBootstrapper _bootstraper;
    private readonly List<IFloxMenuSource> _menuSources = [];

    /// <summary>One menu per module, ordered by position against each other; the items of a menu are never reordered here.</summary>
    /// <param name="user">The current user.</param>
    /// <returns>The menus in display order.</returns>
    public IReadOnlyList<IFloxMenu> GetMenus(ClaimsPrincipal user)
    {
        return _menuSources.Select(source => source.GetMenu(user))
                           .OrderBy(menu => menu.Position)
                           .ToList();
    }

    /// <summary>The Flox assemblies of the host.</summary>
    public IReadOnlyList<Assembly> BlazorAssemblies
    {
        get
        {
            return _bootstraper.FloxAssemblies;
        }
    }
}
