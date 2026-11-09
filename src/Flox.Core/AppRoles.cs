namespace Flox.Core;

// Role names match Orchard Core's built-in roles (OrchardCoreConstants.Roles), so a role from the
// identity provider's token is understood by Orchard's own permission checks as is. Orchard's
// assembly is server-only and can't be referenced from WASM, hence the mirror; the server-side
// OrchardConstantsMirrorTests fail if a value drifts.
public static class AppRoles
{
    // Full access. Assigned in Orchard (Administrator role) and delivered in the token
    public const string ADMINISTRATOR = "Administrator";

    public const string EDITOR = "Editor";

    public const string MODERATOR = "Moderator";

    public const string AUTHOR = "Author";

    public const string CONTRIBUTOR = "Contributor";

    // Flox-specific, not an Orchard role: can manage FHIR resource visualization templates
    public const string DEVELOPER = "Developer";
}

// Orchard's system roles, computed per request and never present in a token
public static class AppSubjects
{
    // Any caller, including anonymous ones: permissions granted to it apply to everyone
    public const string ANONYMOUS = "Anonymous";

    public const string AUTHENTICATED = "Authenticated";

    public static IReadOnlyList<string> All { get; } = [ANONYMOUS, AUTHENTICATED];
}
