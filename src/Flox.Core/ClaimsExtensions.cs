using System.Security.Claims;

namespace Flox.Core;

public static class ClaimsExtensions
{
    public static void AddClaimIfNotExists(this ClaimsIdentity identity, Claim claim)
    {
        var existingClaim = identity.Claims.FirstOrDefault(row => row.Type == claim.Type && row.Value == claim.Value);
        if (existingClaim == null)
        {
            identity.AddClaim(claim);
        }
    }

    // ClaimTypes.Role is the only role claim type in the application: token roles are normalized to it
    // (RoleClaimsMapper), so this agrees with IsInRole and the authorization policies
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

    // Returns OAuth2 sub (NameIdentifier for Orchard's own cookie sessions, which have no sub; the
    // two are the same value in tokens); an unauthenticated principal has no user id
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

    // Returns OAuth2 sub, see the ClaimsPrincipal overload
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

    // Who may see the text and the stack trace of a server failure in the answer: those who debug
    public static bool CanSeeErrorDetails(this ClaimsPrincipal? value)
    {
        if (value == null || value.Identity == null || !value.Identity.IsAuthenticated)
        {
            return false;
        }

        return value.IsInRole(AppRoles.ADMINISTRATOR) || value.IsInRole(AppRoles.DEVELOPER);
    }

    public static bool IsAdmin(this ClaimsPrincipal? value)
    {
        if (value == null || value.Identity == null || !value.Identity.IsAuthenticated)
        {
            return false;
        }

        return value.IsInRole(AppRoles.ADMINISTRATOR);
    }

    public static bool IsAuthenticated(this ClaimsPrincipal? value)
    {
        return value?.Identity?.IsAuthenticated == true;
    }
}
