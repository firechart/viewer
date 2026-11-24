using Flox.Core.Authorization;
using Flox.Shared.Services;

namespace Flox.Shared.Bootstrap.Modules;

/// <summary>
/// Registers the authorization policies, the loader of the effective permissions of the signed-in user, and the access lists of the rows with
/// their dialog.
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
        // The access lists of the server and their dialog, for any page that shows a row
        Services.AddScoped<IAccessListClient>(services => new HttpAccessListClient(services.GetRequiredService<IHttpClientFactory>()));
        Services.AddScoped<IAccessDialogService, AccessDialogService>();
    }
}
