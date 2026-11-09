using Flox.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Authorization;

public abstract class UserAuthorizationHandlerBase<TRequirement> : AuthorizationHandler<TRequirement> where TRequirement : IAuthorizationRequirement
{
    protected UserAuthorizationHandlerBase(IAuthenticationService authenticationService, ILogger<UserAuthorizationHandlerBase<TRequirement>> logger)
    {
        AuthenticationService = authenticationService;
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected ILogger Logger { get; }

    protected IAuthenticationService AuthenticationService { get; }

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

    protected virtual bool RequiresAuthentication()
    {
        return true; // Most policies require authentication
    }

    protected virtual bool IsAuthenticated(AuthorizationHandlerContext context)
    {
        return context.User.IsAuthenticated() == true;
    }

    protected abstract bool IsAuthorized(AuthorizationHandlerContext context);

    protected virtual string GetPolicyName()
    {
        return GetType()
              .Name
              .Replace("Handler", "", StringComparison.InvariantCultureIgnoreCase);
    }

    protected virtual string GetDenialReason(AuthorizationHandlerContext context)
    {
        return "Policy requirements not met";
    }

    protected string GetTokenType(AuthorizationHandlerContext context)
    {
        if (context.User.IsAuthenticated())
        {
            return "Authenticated";
        }

        return "None";
    }
}
