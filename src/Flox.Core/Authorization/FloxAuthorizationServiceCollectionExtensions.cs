using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flox.Core.Authorization;

// Single source of truth for Flox's own authorization policies (ADMIN/AUTHENTICATED/ANONYMOUS/
// DEVELOPER, the dynamic "perm:<Operation>" policy) - called from both the client's
// AuthorizationPoliciesModule (a BootstrapModule) and the server's AuthorizationPoliciesStartup
// (an Orchard StartupBase of the server), which otherwise had this exact policy setup
// duplicated verbatim. Deliberately framework-agnostic (no Blazor-specific registration such as
// AddCascadingAuthenticationState) - callers add that themselves alongside this call, since not
// every IServiceCollection this runs against is building a Blazor render pipeline.
public static class FloxAuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddFloxAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, AuthenticatedUserHandler>();
        services.AddScoped<IAuthorizationHandler, AnonymousUserHandler>();

        services.AddAuthorizationCore(options =>
        {
            options.AddPolicy(FloxPolicies.ADMIN,
                              policy =>
                              {
                                  policy.RequireAuthenticatedUser();
                                  policy.AddRequirements(new AuthenticatedUserRequirement());
                                  policy.RequireRole(AppRoles.ADMINISTRATOR);
                              });
            options.AddRolePolicy(FloxPolicies.DEVELOPER, AppRoles.DEVELOPER);
            options.AddPolicy(FloxPolicies.AUTHENTICATED,
                              policy =>
                              {
                                  policy.RequireAuthenticatedUser();
                                  policy.AddRequirements(new AuthenticatedUserRequirement());
                              });
            options.AddPolicy(FloxPolicies.ANONYMOUS,
                              policy =>
                              {
                                  policy.AddRequirements(new AnonymousReadRequirement());
                              });
            options.DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new AnonymousReadRequirement())
                                                                    .Build();
        });

        return services;
    }

    // Client side of "perm:<Operation>": looks it up in the permissions the server computed for the user
    // (the host adds an IEffectivePermissionsLoader); the server evaluates them in Orchard instead
    public static IServiceCollection AddFloxEffectivePermissions(this IServiceCollection services)
    {
        services.AddScoped<IEffectivePermissionsProvider, EffectivePermissionsProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());
        return services;
    }
}
