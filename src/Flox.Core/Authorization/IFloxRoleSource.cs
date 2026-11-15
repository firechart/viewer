namespace Flox.Core.Authorization;

/// <summary>
/// Well-known roles contributed by the platform or by an application. Register with [FloxService(ServiceType = typeof(IFloxRoleSource), Lifetime = ServiceLifetime.Singleton, Multiple = true)].
/// </summary>
public interface IFloxRoleSource
{
    /// <summary>
    /// Returns the names of the roles.
    /// </summary>
    IReadOnlyList<string> GetRoles();
}
