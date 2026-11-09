using Microsoft.AspNetCore.Authorization;

namespace Flox.Core.Authorization;

public static class AuthorizationOptionsExtensions
{
    // The user must be authenticated and hold at least one of the roles. Admin is not implied:
    // include AppRoles.ADMINISTRATOR explicitly when a policy should also be open to administrators
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
