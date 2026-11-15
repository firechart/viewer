using Flox.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Authorization;

/// <summary>
/// Requirement that is met by any caller, signed in or not.
/// </summary>
public class AnonymousReadRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Authorization handler that lets every caller through.
/// </summary>
public class AnonymousUserHandler : UserAuthorizationHandlerBase<AnonymousReadRequirement>
{
    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="authenticationService">Tells who is signed in.</param>
    /// <param name="logger">Logger of the decisions.</param>
    public AnonymousUserHandler(IAuthenticationService authenticationService, ILogger<AnonymousUserHandler> logger) : base(authenticationService, logger)
    {
    }

    /// <inheritdoc />
    protected override bool RequiresAuthentication()
    {
        return false;
    }

    /// <inheritdoc />
    protected override bool IsAuthorized(AuthorizationHandlerContext context)
    {
        return true;
    }

    /// <inheritdoc />
    protected override string GetDenialReason(AuthorizationHandlerContext context)
    {
        return string.Empty;
    }
}
