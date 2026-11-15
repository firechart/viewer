using Flox.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Authorization;

/// <summary>
/// Base of the handlers that decide by who the caller is; it logs every grant and rejection.
/// </summary>
public abstract class UserAuthorizationHandlerBase<TRequirement> : AuthorizationHandler<TRequirement> where TRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="authenticationService">Tells who is signed in.</param>
    /// <param name="logger">Logger of the decisions.</param>
    protected UserAuthorizationHandlerBase(IAuthenticationService authenticationService, ILogger<UserAuthorizationHandlerBase<TRequirement>> logger)
    {
        AuthenticationService = authenticationService;
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logger of the decisions.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Tells who is signed in.
    /// </summary>
    protected IAuthenticationService AuthenticationService { get; }

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TRequirement requirement)
    {
        var policyName = GetPolicyName();
        var userId = context.User?.GetUserId();
        var userName = context.User?.GetUserName();

        // Some policies (like AnonymousReadRequirement) allow unauthenticated users
        if (RequiresAuthentication() && !IsAuthenticated(context))
        {
            Logger.LogWarning("Authorization: REJECTED (Not Authenticated) - Policy: [{Policy}] User: [{UserId}]", policyName, userId);
            return Task.CompletedTask;
        }

        var isAuthorized = IsAuthorized(context);

        if (isAuthorized)
        {
            Logger.LogInformation("Authorization: GRANTED - Policy: [{Policy}] User: [{UserId}] [{UserName}] TokenType: [{TokenType}]", policyName, userId, userName, GetTokenType(context));

            context.Succeed(requirement);
        }
        else
        {
            Logger.LogWarning("Authorization: DENIED - Policy: [{Policy}] User: [{UserId}] [{UserName}] TokenType: [{TokenType}] Reason: [{Reason}]", policyName, userId, userName, GetTokenType(context), GetDenialReason(context));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Tells whether the policy needs a signed-in user; true by default.
    /// </summary>
    protected virtual bool RequiresAuthentication()
    {
        return true; // Most policies require authentication
    }

    /// <summary>
    /// Tells whether the caller is signed in.
    /// </summary>
    protected virtual bool IsAuthenticated(AuthorizationHandlerContext context)
    {
        return context.User.IsAuthenticated() == true;
    }

    /// <summary>
    /// Decides whether the caller meets the requirement.
    /// </summary>
    protected abstract bool IsAuthorized(AuthorizationHandlerContext context);

    /// <summary>
    /// Name of the policy, for the log.
    /// </summary>
    protected virtual string GetPolicyName()
    {
        return GetType()
              .Name
              .Replace("Handler", "", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Reason to log when the requirement is not met.
    /// </summary>
    protected virtual string GetDenialReason(AuthorizationHandlerContext context)
    {
        return "Policy requirements not met";
    }

    /// <summary>
    /// Kind of token the caller holds, for the log.
    /// </summary>
    protected string GetTokenType(AuthorizationHandlerContext context)
    {
        if (context.User.IsAuthenticated())
        {
            return "Authenticated";
        }

        return "None";
    }
}
