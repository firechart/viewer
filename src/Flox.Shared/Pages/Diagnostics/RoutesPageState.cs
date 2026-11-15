using Flox.Shared.Services;

namespace Flox.Shared.Pages.Diagnostics;

/// <summary>State of the diagnostic page that lists the routes of the application.</summary>
public class RoutesPageState : State
{
    private readonly IRouteDiscoveryService _routeDiscovery;

    /// <summary>Creates the state over the route discovery service.</summary>
    public RoutesPageState(IServiceProvider serviceProvider, IRouteDiscoveryService routeDiscovery) : base(serviceProvider)
    {
        _routeDiscovery = routeDiscovery;
    }

    /// <summary>Every route of the application.</summary>
    public IReadOnlyList<RouteEntry> Routes { get; private set; } = [];

    /// <summary>Text a route must contain (in its template, component or assembly name) to be shown; empty shows all.</summary>
    public string? Filter { get; set; }

    /// <summary>The routes that match <see cref="Filter"/>.</summary>
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

    /// <inheritdoc />
    protected override Task OnStartAsync(object? key)
    {
        Routes = _routeDiscovery.GetRoutes();
        return Task.CompletedTask;
    }
}
