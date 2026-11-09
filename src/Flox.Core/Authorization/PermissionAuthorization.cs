using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Flox.Core.Authorization;

public sealed class PermissionRequirement
(
    string operation
) : IAuthorizationRequirement
{
    public string Operation { get; } = operation;
}

// Clients never evaluate roles or grants: an operation is allowed when the server listed it among
// the signed-in user's effective permissions (GET /api/permissions/effective)
public sealed class PermissionHandler
(
    IEffectivePermissionsProvider permissions
) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var operations = await permissions.GetOperationsAsync();
        if (operations.Contains(requirement.Operation))
        {
            context.Succeed(requirement);
        }
    }
}

// Builds "perm:<Operation>" policies on demand; every other policy name goes to the default provider
// The requirement is Flox's own unless a host supplies another (the server uses Orchard's)
public sealed class PermissionPolicyProvider
(
    IOptions<AuthorizationOptions> options,
    Func<string, IAuthorizationRequirement>? requirementFactory = null
) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new DefaultAuthorizationPolicyProvider(options);
    private readonly Func<string, IAuthorizationRequirement> _requirementFactory = requirementFactory ?? (operation => new PermissionRequirement(operation));

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallback.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallback.GetFallbackPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Permissions.PolicyPrefix, StringComparison.Ordinal))
        {
            var operation = policyName[Permissions.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder().AddRequirements(_requirementFactory(operation))
                                                         .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
