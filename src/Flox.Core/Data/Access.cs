namespace Flox.Core.Data;

/// <summary>
/// Who may read or write a document besides its owner; the default is the safest value. The owner reads the own document whatever the level is.
/// </summary>
public enum Access
{
    /// <summary>The owner only.</summary>
    Owner = 0,

    /// <summary>Anyone, anonymous callers too (reading only).</summary>
    Public = 1,

    /// <summary>The members of one role: <c>ReadRole</c>, <c>WriteRole</c> of the document.</summary>
    Role = 2,

    /// <summary>Any signed-in user.</summary>
    Authenticated = 3
}

/// <summary>
/// A level with the role it needs; an <see cref="Access"/> value converts to a grant without a role.
/// </summary>
/// <param name="Level">The access level.</param>
/// <param name="Role">The role that gets access when the level is <see cref="Access.Role"/>.</param>
public readonly record struct AccessGrant(Access Level, string? Role = null)
{
    /// <summary>Converts a level to a grant without a role.</summary>
    public static implicit operator AccessGrant(Access level)
    {
        return FromAccess(level);
    }

    /// <summary>Creates a grant of the level without a role.</summary>
    /// <param name="level">The access level.</param>
    public static AccessGrant FromAccess(Access level)
    {
        return new AccessGrant(level);
    }

    /// <summary>Creates a grant of <see cref="Access.Role"/> for the role.</summary>
    /// <param name="role">The role that gets access.</param>
    public static AccessGrant OfRole(string role)
    {
        return new AccessGrant(Access.Role, role);
    }
}
