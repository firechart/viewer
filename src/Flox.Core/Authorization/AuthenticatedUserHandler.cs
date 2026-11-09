using Flox.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Authorization;

public class AuthenticatedUserRequirement : IAuthorizationRequirement
{
}

public class AuthenticatedUserHandler : UserAuthorizationHandlerBase<AuthenticatedUserRequirement>
{
    public AuthenticatedUserHandler(IAuthenticationService authenticationService, ILogger<AuthenticatedUserHandler> logger) : base(authenticationService, logger)
    {
    }

    protected override string GetDenialReason(AuthorizationHandlerContext context)
    {
        return "Authenticated user required";
    }

    protected override bool IsAuthorized(AuthorizationHandlerContext context)
    {
        return context.User.IsAuthenticated();
    }
}
