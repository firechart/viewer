using Flox.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Authorization;

/// <summary>
/// Requirement that is met by a signed-in user.
/// </summary>
public class AuthenticatedUserRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Authorization handler that lets a signed-in user through.
/// </summary>
public class AuthenticatedUserHandler : UserAuthorizationHandlerBase<AuthenticatedUserRequirement>
{
    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="authenticationService">Tells who is signed in.</param>
    /// <param name="logger">Logger of the decisions.</param>
    public AuthenticatedUserHandler(IAuthenticationService authenticationService, ILogger<AuthenticatedUserHandler> logger) : base(authenticationService, logger)
    {
    }

    /// <inheritdoc />
    protected override string GetDenialReason(AuthorizationHandlerContext context)
    {
        return "Authenticated user required";
    }

    /// <inheritdoc />
    protected override bool IsAuthorized(AuthorizationHandlerContext context)
    {
        return context.User.IsAuthenticated();
    }
}
