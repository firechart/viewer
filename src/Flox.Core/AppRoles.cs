namespace Flox.Core;

/// <summary>
/// Names of the standard roles. They match Orchard Core's built-in roles, so a role from the identity provider's token is understood by Orchard's permission checks as is.
/// </summary>
public static class AppRoles
{
    /// <summary>
    /// Full access. Assigned in Orchard (Administrator role) and delivered in the token.
    /// </summary>
    public const string ADMINISTRATOR = "Administrator";

    /// <summary>
    /// Role that edits content of other users.
    /// </summary>
    public const string EDITOR = "Editor";

    /// <summary>
    /// Role that moderates content.
    /// </summary>
    public const string MODERATOR = "Moderator";

    /// <summary>
    /// Role that creates and edits its own content.
    /// </summary>
    public const string AUTHOR = "Author";

    /// <summary>
    /// Role that contributes content for review.
    /// </summary>
    public const string CONTRIBUTOR = "Contributor";

    /// <summary>
    /// Flox-specific role (not an Orchard role) that can manage the FHIR resource visualization templates.
    /// </summary>
    public const string DEVELOPER = "Developer";
}

/// <summary>
/// Orchard's system roles, computed per request and never present in a token.
/// </summary>
public static class AppSubjects
{
    /// <summary>
    /// Any caller, including anonymous ones: permissions granted to it apply to everyone.
    /// </summary>
    public const string ANONYMOUS = "Anonymous";

    /// <summary>
    /// Any signed-in user.
    /// </summary>
    public const string AUTHENTICATED = "Authenticated";

    /// <summary>
    /// Both system roles.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [ANONYMOUS, AUTHENTICATED];
}
