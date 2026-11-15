using Microsoft.Extensions.DependencyInjection;

namespace Flox.Core.Host;

/// <summary>Marks a class to be registered in the service collection by the bootstrapper scan.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FloxServiceAttribute : Attribute
{
    /// <summary>Registers the class as the given service type.</summary>
    /// <param name="serviceType">The service type the class is registered as.</param>
    /// <param name="lifetime">The lifetime of the service.</param>
    public FloxServiceAttribute(Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ServiceType = serviceType;
        Lifetime = lifetime;
    }

    /// <summary>Registers the class as itself.</summary>
    /// <param name="lifetime">The lifetime of the service.</param>
    public FloxServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        Lifetime = lifetime;
    }

    /// <summary>The lifetime of the service.</summary>
    public ServiceLifetime Lifetime { get; } = ServiceLifetime.Scoped;
    /// <summary>The service type the class is registered as; the class itself when null.</summary>
    public Type? ServiceType { get; }

    /// <summary>Several implementations of one service type (sources, option configurators): registered additively instead of first-wins, so an app can extend what the platform registers.</summary>
    public bool Multiple { get; set; }
}
