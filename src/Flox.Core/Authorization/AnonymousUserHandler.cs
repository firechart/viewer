using Flox.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Authorization;

public class AnonymousReadRequirement : IAuthorizationRequirement
{
}

public class AnonymousUserHandler : UserAuthorizationHandlerBase<AnonymousReadRequirement>
{
    public AnonymousUserHandler(IAuthenticationService authenticationService, ILogger<AnonymousUserHandler> logger) : base(authenticationService, logger)
    {
    }

    protected override bool RequiresAuthentication()
    {
        return false;
    }

    protected override bool IsAuthorized(AuthorizationHandlerContext context)
    {
        return true;
    }

    protected override string GetDenialReason(AuthorizationHandlerContext context)
    {
        return string.Empty;
    }
}
