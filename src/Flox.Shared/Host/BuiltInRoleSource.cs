using Flox.Core.Authorization;

namespace Flox.Shared.Host;

/// <summary>Declares the roles every Flox application has (administrator and developer).</summary>
[FloxService(typeof(IFloxRoleSource), ServiceLifetime.Singleton, Multiple = true)]
public sealed class BuiltInRoleSource : IFloxRoleSource
{
    /// <inheritdoc />
    public IReadOnlyList<string> GetRoles()
    {
        return [AppRoles.ADMINISTRATOR, AppRoles.DEVELOPER];
    }
}
