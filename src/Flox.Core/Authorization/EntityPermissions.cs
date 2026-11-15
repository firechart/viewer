using System.Collections.Concurrent;

namespace Flox.Core.Authorization;

/// <summary>
/// Names of the operations on the table of an entity.
/// </summary>
public static class EntityOperations
{
    /// <summary>
    /// Reading rows of the table.
    /// </summary>
    public const string Read = nameof(Read);
    /// <summary>
    /// Creating rows in the table.
    /// </summary>
    public const string Create = nameof(Create);
    /// <summary>
    /// Changing rows of the table.
    /// </summary>
    public const string Update = nameof(Update);
    /// <summary>
    /// Deleting rows from the table.
    /// </summary>
    public const string Delete = nameof(Delete);

    /// <summary>
    /// Opening a document wider than its owner and the owner's roles: to all signed-in users, to everyone, to a role the owner does not hold. Nobody has it by default but the administrators; nothing asks for it yet.
    /// </summary>
    public const string Publish = nameof(Publish);

    /// <summary>
    /// The operations the storage asks for.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Read, Create, Update, Delete];

    /// <summary>
    /// The operations a table registers as Orchard permissions.
    /// </summary>
    public static readonly IReadOnlyList<string> Registered = [.. All, Publish];
}

/// <summary>
/// What the roles get by default for the table of an entity, once, when the site meets the entity; the administrator changes it on the page of the roles.
/// </summary>
[Flags]
public enum EntityGrants
{
    /// <summary>
    /// Nobody: the server code uses the table.
    /// </summary>
    None = 0,

    /// <summary>
    /// Signed-in users read the table.
    /// </summary>
    AuthenticatedRead = 1,

    /// <summary>
    /// Signed-in users create, change and delete in the table; the rows keep their own rights.
    /// </summary>
    AuthenticatedWrite = 2,

    /// <summary>
    /// Anonymous callers read the table.
    /// </summary>
    AnonymousRead = 4,

    /// <summary>
    /// The writes to the table are made by a service that checks an operation of its own: the storage asks for the permission to read only.
    /// </summary>
    ServiceWrites = 8,

    /// <summary>
    /// The data of a user.
    /// </summary>
    UserData = AuthenticatedRead | AuthenticatedWrite,

    /// <summary>
    /// A table that everyone reads and the server fills.
    /// </summary>
    PublicRead = AuthenticatedRead | AnonymousRead | ServiceWrites
}

/// <summary>
/// The first layer of access to the data: whether a role may use the table at all (Orchard's permissions, granted on the page of the roles). The second layer is the rights of the row (EntityAccessUtil); an operation needs both.
/// </summary>
public static class EntityPermissions
{
    private static readonly ConcurrentDictionary<Type, EntityGrants> s_grants = new();

    /// <summary>
    /// Returns the name of the Orchard permission for an operation on an entity type.
    /// </summary>
    public static string Name(Type entityType, string operation)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return $"{entityType.Name}.{operation}";
    }

    /// <summary>
    /// Records the default grants of an entity type, so the storage knows which operations to check.
    /// </summary>
    public static void Register(Type entityType, EntityGrants grants)
    {
        s_grants[entityType] = grants;
    }

    /// <summary>
    /// Tells whether the storage checks the operation on the table: a table of a service is checked for reading only, an unknown one (a test entity) is not checked.
    /// </summary>
    public static bool IsChecked(Type entityType, string operation)
    {
        if (!s_grants.TryGetValue(entityType, out var grants))
        {
            return false;
        }

        return operation == EntityOperations.Read || !grants.HasFlag(EntityGrants.ServiceWrites);
    }
}

/// <summary>
/// A permission granted to a role by default.
/// </summary>
/// <param name="Role">Name of the role.</param>
/// <param name="Permission">Name of the permission.</param>
public sealed record DefaultGrant(string Role, string Permission);

/// <summary>
/// Source of the grants a site gets for an operation source; seeded once per grant, so a grant the administrator took away stays taken.
/// </summary>
public interface IFloxDefaultGrantSource
{
    /// <summary>
    /// Returns the grants to seed.
    /// </summary>
    IReadOnlyList<DefaultGrant> GetDefaultGrants();
}

/// <summary>
/// Registers the operations of an entity table as permissions and tells what the roles get by default.
/// </summary>
/// <param name="entityType">The entity type of the table.</param>
/// <param name="grants">What the roles get by default.</param>
public sealed class EntityOperationSource(Type entityType, EntityGrants grants) : IFloxOperationSource, IFloxDefaultGrantSource
{
    /// <inheritdoc />
    public string Category
    {
        get
        {
            return "Flox data";
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetOperations()
    {
        return [.. EntityOperations.Registered.Select(operation => EntityPermissions.Name(entityType, operation))];
    }

    /// <inheritdoc />
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
