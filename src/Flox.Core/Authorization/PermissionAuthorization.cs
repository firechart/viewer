using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Flox.Core.Authorization;

/// <summary>
/// Requirement that the signed-in user holds an operation.
/// </summary>
/// <param name="operation">Name of the operation.</param>
public sealed class PermissionRequirement
(
    string operation
) : IAuthorizationRequirement
{
    /// <summary>
    /// Name of the required operation.
    /// </summary>
    public string Operation { get; } = operation;
}

/// <summary>
/// Handler of PermissionRequirement. Clients never evaluate roles or grants: an operation is allowed when the server listed it among the signed-in user's effective permissions (GET /api/permissions/effective).
/// </summary>
/// <param name="permissions">Source of the effective permissions.</param>
public sealed class PermissionHandler
(
    IEffectivePermissionsProvider permissions
) : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var operations = await permissions.GetOperationsAsync();
        if (operations.Contains(requirement.Operation))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>
/// Builds the "perm:Operation" policies on demand; every other policy name goes to the default provider. The requirement is Flox's own unless a host supplies another (the server uses Orchard's).
/// </summary>
/// <param name="options">Authorization options for the default provider.</param>
/// <param name="requirementFactory">Creates the requirement for an operation; Flox's own when omitted.</param>
public sealed class PermissionPolicyProvider
(
    IOptions<AuthorizationOptions> options,
    Func<string, IAuthorizationRequirement>? requirementFactory = null
) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new DefaultAuthorizationPolicyProvider(options);
    private readonly Func<string, IAuthorizationRequirement> _requirementFactory = requirementFactory ?? (operation => new PermissionRequirement(operation));

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallback.GetDefaultPolicyAsync();
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallback.GetFallbackPolicyAsync();
    }

    /// <inheritdoc />
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
