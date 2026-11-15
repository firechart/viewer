using System.Security.Claims;

namespace Flox.Core;

/// <summary>
/// Helpers to read the identity of the caller from a ClaimsPrincipal.
/// </summary>
public static class ClaimsExtensions
{
    /// <summary>
    /// Adds the claim unless the identity already has a claim of the same type and value.
    /// </summary>
    public static void AddClaimIfNotExists(this ClaimsIdentity identity, Claim claim)
    {
        var existingClaim = identity.Claims.FirstOrDefault(row => row.Type == claim.Type && row.Value == claim.Value);
        if (existingClaim == null)
        {
            identity.AddClaim(claim);
        }
    }

    /// <summary>
    /// Returns the sorted, distinct roles of the principal. ClaimTypes.Role is the only role claim type in the application: token roles are normalized to it (RoleClaimsMapper), so this agrees with IsInRole and the authorization policies.
    /// </summary>
    public static List<string> GetRoles(this ClaimsPrincipal? value)
    {
        if (value == null)
        {
            return [];
        }

        var result = value.FindAll(ClaimTypes.Role)
                          .Select(row => row.Value)
                          .Distinct()
                          .ToList();
        result.Sort();
        return result;
    }

    /// <summary>
    /// Returns the user id: the OAuth2 sub (the NameIdentifier for Orchard's own cookie sessions, which have no sub; the two are the same value in tokens). An unauthenticated principal has no user id and gets the empty one.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal? value)
    {
        if (value?.Identity?.IsAuthenticated != true)
        {
            return Constants.NULL_GUID;
        }

        return value.FindFirst(Constants.SUB_CLAIM)
                   ?.Value ?? value.FindFirst(ClaimTypes.NameIdentifier)
                                  ?.Value ?? Constants.NULL_GUID;
    }

    /// <summary>
    /// Returns the user id of the identity, see the ClaimsPrincipal overload.
    /// </summary>
    public static string? GetUserId(this ClaimsIdentity? value)
    {
        if (value == null)
        {
            return Constants.NULL_GUID;
        }

        if (!value.IsAuthenticated)
        {
            return Constants.NULL_GUID;
        }

        if (value.Claims == null || !value.Claims.Any())
        {
            return Constants.NULL_GUID;
        }

        var claim = value.FindFirst(row => row.Type == Constants.SUB_CLAIM) ?? value.FindFirst(ClaimTypes.NameIdentifier);
        return claim?.Value ?? Constants.NULL_GUID;
    }

    /// <summary>
    /// Returns the display name of the user, or null when the principal is not authenticated.
    /// </summary>
    public static string? GetUserName(this ClaimsPrincipal? value)
    {
        if (value == null)
        {
            return null;
        }

        var identity = value.Identity as ClaimsIdentity;
        if (identity == null)
        {
            return null;
        }

        return GetUserName(identity);
    }

    /// <summary>
    /// Returns the display name of the user (name, preferred_username, name claim or email), or null when the identity is not authenticated.
    /// </summary>
    public static string? GetUserName(this ClaimsIdentity? value)
    {
        if (value == null)
        {
            return null;
        }

        if (!value.IsAuthenticated)
        {
            return null;
        }

        if (value.Claims == null || !value.Claims.Any())
        {
            return null;
        }

        var result = value.Name ?? value.FindFirst("preferred_username")
                                       ?.Value ?? value.FindFirst("name")
                                                      ?.Value ?? value.FindFirst(ClaimTypes.Email)
                                                                     ?.Value ?? "?";
        return result;
    }

    /// <summary>
    /// Tells whether the caller may see the text and the stack trace of a server failure in the answer: those who debug.
    /// </summary>
    public static bool CanSeeErrorDetails(this ClaimsPrincipal? value)
    {
        if (value == null || value.Identity == null || !value.Identity.IsAuthenticated)
        {
            return false;
        }

        return value.IsInRole(AppRoles.ADMINISTRATOR) || value.IsInRole(AppRoles.DEVELOPER);
    }

    /// <summary>
    /// Tells whether the principal is an authenticated administrator.
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal? value)
    {
        if (value == null || value.Identity == null || !value.Identity.IsAuthenticated)
        {
            return false;
        }

        return value.IsInRole(AppRoles.ADMINISTRATOR);
    }

    /// <summary>
    /// Tells whether the principal is authenticated.
    /// </summary>
    public static bool IsAuthenticated(this ClaimsPrincipal? value)
    {
        return value?.Identity?.IsAuthenticated == true;
    }
}
