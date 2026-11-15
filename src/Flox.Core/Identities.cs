using System.Security.Claims;

namespace Flox.Core;

/// <summary>
/// Built-in identities.
/// </summary>
public static class Identities
{
    /// <summary>
    /// The identity of an anonymous caller.
    /// </summary>
    public static ClaimsPrincipal DefaultIdentity { get; } = new ClaimsPrincipal(new ClaimsIdentity());

    /// <summary>
    /// Guest administrator, only for testing. Actually the identity doesn't have superuser privileges
    /// </summary>
    public static ClaimsPrincipal Administrator { get; } = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
                                                                                                  {
                                                                                                      new Claim(Constants.SUB_CLAIM, Constants.DEFAULT_ADMIN_ID.ToString()), new Claim(Constants.NAME_CLAIM, nameof(Administrator)), new Claim(ClaimTypes.Role, AppRoles.ADMINISTRATOR)
                                                                                                  },
                                                                                                  Constants.AUTHENTICATION_TYPE));

    /// <summary>
    /// The identity of the platform's own services.
    /// </summary>
    public static ClaimsPrincipal Service { get; } = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
                                                                                            {
                                                                                                new Claim(Constants.SUB_CLAIM, Constants.DEFAULT_SERVICE_ID.ToString()), new Claim(Constants.NAME_CLAIM, "Background Service"), new Claim(ClaimTypes.Role, AppRoles.ADMINISTRATOR)
                                                                                            },
                                                                                            Constants.AUTHENTICATION_TYPE));
}
