using System.Reflection;

namespace Flox.Shared.Services;

/// <summary>
/// A route of the application: the template of a routable component.
/// </summary>
/// <param name="Template">The route template of the page.</param>
/// <param name="ComponentType">The component that serves the route.</param>
/// <param name="Assembly">The assembly of the component.</param>
public sealed record RouteEntry
(
    string Template,
    Type ComponentType,
    Assembly Assembly
);

/// <summary>Finds the routable pages of the application.</summary>
public interface IRouteDiscoveryService
{
    /// <summary>Returns every route of the application, ordered by template.</summary>
    IReadOnlyList<RouteEntry> GetRoutes();
}

/// <summary>Finds the routes by scanning the Blazor assemblies of the shell for components with a route attribute.</summary>
[FloxService(typeof(IRouteDiscoveryService))]
public sealed class RouteDiscoveryService : IRouteDiscoveryService
{
    private readonly IFloxShell _registry;

    /// <summary>Creates the service over the shell, which knows the Blazor assemblies.</summary>
    public RouteDiscoveryService(IFloxShell registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public IReadOnlyList<RouteEntry> GetRoutes()
    {
        return _registry.BlazorAssemblies
                        .SelectMany(a => a.GetTypes())
                        .Where(t => t.IsAssignableTo(typeof(IComponent)) && !t.IsAbstract)
                        .SelectMany(t => t.GetCustomAttributes<RouteAttribute>()
                                          .Select(r => new RouteEntry(r.Template, t, t.Assembly)))
                        .OrderBy(r => r.Template)
                        .ToList();
    }
}
