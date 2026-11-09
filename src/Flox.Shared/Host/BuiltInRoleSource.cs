using Flox.Core.Authorization;

namespace Flox.Shared.Host;

[FloxService(typeof(IFloxRoleSource), ServiceLifetime.Singleton, Multiple = true)]
public sealed class BuiltInRoleSource : IFloxRoleSource
{
    public IReadOnlyList<string> GetRoles()
    {
        return [AppRoles.ADMINISTRATOR, AppRoles.DEVELOPER];
    }
}
