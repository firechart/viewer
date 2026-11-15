namespace Flox.Core.Authorization;

/// <summary>
/// Names of the Flox authorization policies.
/// </summary>
public static class FloxPolicies
{
    /// <summary>
    /// Administrators only.
    /// </summary>
    public const string ADMIN = nameof(ADMIN);
    /// <summary>
    /// Any caller, signed in or not.
    /// </summary>
    public const string ANONYMOUS = nameof(ANONYMOUS);
    /// <summary>
    /// Any signed-in user.
    /// </summary>
    public const string AUTHENTICATED = nameof(AUTHENTICATED);
    /// <summary>
    /// Users in the Developer role.
    /// </summary>
    public const string DEVELOPER = nameof(DEVELOPER);
}
