using Microsoft.Extensions.DependencyInjection;

namespace Flox.Core.Host;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FloxServiceAttribute : Attribute
{
    public FloxServiceAttribute(Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ServiceType = serviceType;
        Lifetime = lifetime;
    }

    public FloxServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        Lifetime = lifetime;
    }

    public ServiceLifetime Lifetime { get; } = ServiceLifetime.Scoped;
    public Type? ServiceType { get; }

    // Several implementations of one service type (sources, option configurators); registered
    // additively instead of first-wins, so an app can extend what the platform registers
    public bool Multiple { get; set; }
}
