using System.Collections.Concurrent;

namespace Flox.Core.Authorization;

public static class EntityOperations
{
    public const string Read = nameof(Read);
    public const string Create = nameof(Create);
    public const string Update = nameof(Update);
    public const string Delete = nameof(Delete);

    // To open a document wider than its owner and the roles the owner holds: to all signed-in users, to everyone, to a role the owner does not hold.
    // Nobody has it by default but the administrators; nothing asks for it yet (the owner cannot change the rights of a document yet)
    public const string Publish = nameof(Publish);

    // What the storage asks for
    public static readonly IReadOnlyList<string> All = [Read, Create, Update, Delete];

    // What a table registers as Orchard permissions
    public static readonly IReadOnlyList<string> Registered = [.. All, Publish];
}

// What the roles get by default for the table of an entity, once, when the site meets the entity (the administrator changes it on the page of the roles)
[Flags]
public enum EntityGrants
{
    // Nobody: the server code uses the table
    None = 0,

    // Signed-in users read the table
    AuthenticatedRead = 1,

    // Signed-in users create, change and delete in the table (the rows keep their own rights)
    AuthenticatedWrite = 2,

    // Anonymous callers read the table
    AnonymousRead = 4,

    // The writes to the table are made by a service that checks an operation of its own: the storage asks for the permission to read only
    ServiceWrites = 8,

    // The data of a user
    UserData = AuthenticatedRead | AuthenticatedWrite,

    // A table that everyone reads and the server fills
    PublicRead = AuthenticatedRead | AnonymousRead | ServiceWrites
}

// The first layer of the access to the data: whether the role may use the table at all (Orchard's permissions, granted on the page of the roles).
// The second layer is the rights of the row (EntityAccessUtil); an operation needs both
public static class EntityPermissions
{
    private static readonly ConcurrentDictionary<Type, EntityGrants> s_grants = new();

    public static string Name(Type entityType, string operation)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return $"{entityType.Name}.{operation}";
    }

    public static void Register(Type entityType, EntityGrants grants)
    {
        s_grants[entityType] = grants;
    }

    // The operations of the table that the storage checks: a table of a service is checked for reading only, an unknown one (a test entity) is not checked
    public static bool IsChecked(Type entityType, string operation)
    {
        if (!s_grants.TryGetValue(entityType, out var grants))
        {
            return false;
        }

        return operation == EntityOperations.Read || !grants.HasFlag(EntityGrants.ServiceWrites);
    }
}

public sealed record DefaultGrant(string Role, string Permission);

// The grants that a site gets for an operation source; seeded once per grant, so that a grant the administrator took away stays taken
public interface IFloxDefaultGrantSource
{
    IReadOnlyList<DefaultGrant> GetDefaultGrants();
}

public sealed class EntityOperationSource(Type entityType, EntityGrants grants) : IFloxOperationSource, IFloxDefaultGrantSource
{
    public string Category
    {
        get
        {
            return "Flox data";
        }
    }

    public IReadOnlyList<string> GetOperations()
    {
        return [.. EntityOperations.Registered.Select(operation => EntityPermissions.Name(entityType, operation))];
    }

    public IReadOnlyList<DefaultGrant> GetDefaultGrants()
    {
        List<DefaultGrant> result = [];
        if (grants.HasFlag(EntityGrants.AnonymousRead))
        {
            result.Add(new DefaultGrant(AppSubjects.ANONYMOUS, EntityPermissions.Name(entityType, EntityOperations.Read)));
        }

        if (grants.HasFlag(EntityGrants.AuthenticatedRead))
        {
            result.Add(new DefaultGrant(AppSubjects.AUTHENTICATED, EntityPermissions.Name(entityType, EntityOperations.Read)));
        }

        if (grants.HasFlag(EntityGrants.AuthenticatedWrite))
        {
            result.AddRange(new[] { EntityOperations.Create, EntityOperations.Update, EntityOperations.Delete }.Select(operation => new DefaultGrant(AppSubjects.AUTHENTICATED, EntityPermissions.Name(entityType, operation))));
        }

        return result;
    }
}
