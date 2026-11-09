namespace Flox.Core.Data;

// Who may read or write a document besides its owner; the default is the safest value. The owner reads the own document whatever the level is
public enum Access
{
    // The owner only
    Owner = 0,

    // Anyone, anonymous callers too (reading only)
    Public = 1,

    // The members of one role: ReadRole, WriteRole of the document
    Role = 2,

    // Any signed-in user
    Authenticated = 3
}

// A level with the role it needs; an Access value converts to a grant without a role
public readonly record struct AccessGrant(Access Level, string? Role = null)
{
    public static implicit operator AccessGrant(Access level)
    {
        return FromAccess(level);
    }

    public static AccessGrant FromAccess(Access level)
    {
        return new AccessGrant(level);
    }

    public static AccessGrant OfRole(string role)
    {
        return new AccessGrant(Access.Role, role);
    }
}
