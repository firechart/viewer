using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Flox.Core.Services;

/// <summary>
/// Should be scoped
/// See also ICurrentPrincipalProvider
/// </summary>
public interface IAuthenticationService
{
#pragma warning disable CA1003
    event AuthenticationStateChangedHandler? AuthenticationStateChanged;
#pragma warning restore CA1003

    Task<string?> GetAccessTokenAsync();

    // The server refused the access token that was sent (401) although it looked good (its clock, a revocation, a renewal in another tab). A token other
    // than the rejected one: one that was renewed meanwhile, or a new one made now without waiting for the expiry. Null: there is none (the default: a
    // client that cannot renew), and the user has to sign in again
    Task<string?> RefreshAccessTokenAsync(string rejectedToken)
    {
        return Task.FromResult<string?>(null);
    }

    Task<ClaimsPrincipal> GetCurrentUserAsync();

    Task GoToProfileAsync();

    Task LoginAsync(string? returnUrl = null);

    Task LogoutAsync();

    void SetCurrentUser(ClaimsPrincipal? value);
}
