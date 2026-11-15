using Microsoft.AspNetCore.Authorization;

namespace Flox.Core.Authorization;

/// <summary>
/// Helpers for registering authorization policies.
/// </summary>
public static class AuthorizationOptionsExtensions
{
    /// <summary>
    /// Adds a policy that needs a signed-in user holding at least one of the roles. Administrator is not implied: include it explicitly when the policy should be open to administrators.
    /// </summary>
    public static AuthorizationOptions AddRolePolicy(this AuthorizationOptions options, string name, params string[] roles)
    {
        options.AddPolicy(name,
                          policy =>
                          {
                              policy.RequireAuthenticatedUser();
                              policy.AddRequirements(new AuthenticatedUserRequirement());
                              policy.RequireRole(roles);
                          });
        return options;
    }
}
