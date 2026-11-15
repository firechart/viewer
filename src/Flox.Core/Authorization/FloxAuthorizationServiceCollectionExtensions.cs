using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flox.Core.Authorization;

/// <summary>
/// Single source of the Flox authorization policies (ADMIN, AUTHENTICATED, ANONYMOUS, DEVELOPER and the dynamic "perm:Operation" policy), shared by the client and the server. Framework-agnostic: callers add the Blazor-specific registration themselves.
/// </summary>
public static class FloxAuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Flox authorization handlers and the named policies.
    /// </summary>
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

    /// <summary>
    /// Registers the client side of the "perm:Operation" policy: it looks the operation up in the permissions the server computed for the user (the host adds an IEffectivePermissionsLoader). The server evaluates them in Orchard instead.
    /// </summary>
    public static IServiceCollection AddFloxEffectivePermissions(this IServiceCollection services)
    {
        services.AddScoped<IEffectivePermissionsProvider, EffectivePermissionsProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());
        return services;
    }
}
