using Flox.Shared.Services;

namespace Flox.Shared.Pages.Diagnostics;

public class RoutesPageState : State
{
    private readonly IRouteDiscoveryService _routeDiscovery;

    public RoutesPageState(IServiceProvider serviceProvider, IRouteDiscoveryService routeDiscovery) : base(serviceProvider)
    {
        _routeDiscovery = routeDiscovery;
    }

    public IReadOnlyList<RouteEntry> Routes { get; private set; } = [];

    public string? Filter { get; set; }

    public IEnumerable<RouteEntry> FilteredRoutes
    {
        get
        {
            return string.IsNullOrWhiteSpace(Filter)
                ? Routes
                : Routes.Where(r => r.Template.Contains(Filter, StringComparison.OrdinalIgnoreCase) || r.ComponentType.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase) || r.Assembly
                                                                                                                                                                                     .GetName()
                                                                                                                                                                                     .Name
                                                                                                                                                                                    ?.Contains(Filter, StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    protected override Task OnStartAsync(object? key)
    {
        Routes = _routeDiscovery.GetRoutes();
        return Task.CompletedTask;
    }
}
