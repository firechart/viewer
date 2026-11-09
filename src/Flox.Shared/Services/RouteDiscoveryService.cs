using System.Reflection;

namespace Flox.Shared.Services;

public sealed record RouteEntry
(
    string Template,
    Type ComponentType,
    Assembly Assembly
);

public interface IRouteDiscoveryService
{
    IReadOnlyList<RouteEntry> GetRoutes();
}

[FloxService(typeof(IRouteDiscoveryService))]
public sealed class RouteDiscoveryService : IRouteDiscoveryService
{
    private readonly IFloxShell _registry;

    public RouteDiscoveryService(IFloxShell registry)
    {
        _registry = registry;
    }

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
