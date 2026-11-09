namespace Flox.Core.Authorization;

// Well-known roles contributed by the platform or by an application; register with
// [FloxService(ServiceType = typeof(IFloxRoleSource), Lifetime = ServiceLifetime.Singleton, Multiple = true)]
public interface IFloxRoleSource
{
    IReadOnlyList<string> GetRoles();
}
