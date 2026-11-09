using Flox.Core.Authorization;
using Flox.Shared.Services;

namespace Flox.Shared.Bootstrap.Modules;

public class AuthorizationPoliciesModule : BootstrapModule
{
    public override void ConfigureServices()
    {
        Services.AddCascadingAuthenticationState();
        Services.AddMemoryCache();
        Services.AddFloxAuthorizationPolicies();
        Services.AddFloxEffectivePermissions();
        Services.AddScoped<IEffectivePermissionsLoader, HttpEffectivePermissionsLoader>();
    }
}
