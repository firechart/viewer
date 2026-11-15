using Flox.Core.Authorization;
using Flox.Shared.Services;

namespace Flox.Shared.Bootstrap.Modules;

/// <summary>
/// Registers the authorization policies and the loader of the effective permissions of the signed-in user.
/// </summary>
public class AuthorizationPoliciesModule : BootstrapModule
{
    /// <inheritdoc />
    public override void ConfigureServices()
    {
        Services.AddCascadingAuthenticationState();
        Services.AddMemoryCache();
        Services.AddFloxAuthorizationPolicies();
        Services.AddFloxEffectivePermissions();
        Services.AddScoped<IEffectivePermissionsLoader, HttpEffectivePermissionsLoader>();
    }
}
