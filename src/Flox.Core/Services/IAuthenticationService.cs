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
    /// <summary>Raised when the authentication state changes.</summary>
    event AuthenticationStateChangedHandler? AuthenticationStateChanged;
#pragma warning restore CA1003

    /// <summary>Gets the access token of the signed-in user.</summary>
    /// <returns>The token, or null when nobody is signed in.</returns>
    Task<string?> GetAccessTokenAsync();

    /// <summary>Asks for a new access token after the server refused the one that was sent (401) although it looked good: its clock, a revocation, a renewal in another tab. Does nothing by default, so the user has to sign in again.</summary>
    /// <param name="rejectedToken">The token the server refused.</param>
    /// <returns>A token other than the rejected one, renewed meanwhile or made now; null when there is none.</returns>
    Task<string?> RefreshAccessTokenAsync(string rejectedToken)
    {
        return Task.FromResult<string?>(null);
    }

    /// <summary>Gets the current user.</summary>
    /// <returns>The user; an anonymous one when nobody is signed in.</returns>
    Task<ClaimsPrincipal> GetCurrentUserAsync();

    /// <summary>Opens the profile page of the identity provider.</summary>
    Task GoToProfileAsync();

    /// <summary>Starts the sign-in.</summary>
    /// <param name="returnUrl">Where to go after signing in.</param>
    Task LoginAsync(string? returnUrl = null);

    /// <summary>Signs the user out.</summary>
    Task LogoutAsync();

    /// <summary>Replaces the current user.</summary>
    /// <param name="value">The user, or null for none.</param>
    void SetCurrentUser(ClaimsPrincipal? value);
}
