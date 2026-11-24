using System.Collections.Concurrent;
using Flox.Core.Data;

namespace Flox.Core.Authorization;

/// <summary>
/// Names of the operations every entity has. An operation of an entity is the Orchard permission <c>&lt;Entity&gt;.&lt;Operation&gt;</c>; an entity
/// may declare more of its own when it is registered (<see cref="EntityAccessOptions.Operations"/>).
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
    /// Reading and changing the access list of a row. Granted to nobody by default but the administrators.
    /// </summary>
    public const string ManageAccess = nameof(ManageAccess);

    /// <summary>
    /// The operations the storage asks for on the table.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Read, Create, Update, Delete];

    /// <summary>
    /// The operations every entity registers as Orchard permissions.
    /// </summary>
    public static readonly IReadOnlyList<string> Base = [.. All, ManageAccess];

    /// <summary>
    /// The operations a row of an entity can allow: every operation but <see cref="Create"/>, which is of the table.
    /// </summary>
    public static IEnumerable<string> OfRow(IEnumerable<string> operations)
    {
        return operations.Where(operation => operation != Create);
    }
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
    /// Signed-in users create, change and delete in the table; the rows keep their own access lists.
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

/// <summary>The kind of an entity, given by the interface it implements; every registered entity has exactly one.</summary>
public enum EntityAccessKind
{
    /// <summary>An access list of its own (<see cref="IAclEntity"/>).</summary>
    Acl,

    /// <summary>The access list of its master (<see cref="IAclDetail{TMaster}"/>).</summary>
    Detail,

    /// <summary>The permissions of the table alone (<see cref="ITableAccess"/>).</summary>
    Table
}

/// <summary>
/// How the access to an entity is declared when it is registered: what the roles get, its own operations, the sensitive ones and what a new
/// row gets. Whether its rows have access lists is its kind (<see cref="EntityAccessKind"/>), not an option.
/// </summary>
public sealed class EntityAccessOptions
{
    /// <summary>What the roles get by default for the table.</summary>
    public EntityGrants Grants { get; init; } = EntityGrants.UserData;

    /// <summary>The operations of the entity beyond <see cref="EntityOperations.Base"/>, such as <c>Pseudonymize</c>.</summary>
    public IReadOnlyList<string> Operations { get; init; } = [];

    /// <summary>
    /// The operations that need an explicit entry of the access list even for an administrator; they are not in the default list of a new row.
    /// </summary>
    public IReadOnlyList<string> Sensitive { get; init; } = [];

    /// <summary>
    /// The access list a new row of an <see cref="IAclEntity"/> gets when its writer gives none; null is the owner with every operation that is
    /// not sensitive.
    /// </summary>
    public IReadOnlyList<AclEntry>? DefaultAcl { get; init; }
}

/// <summary>
/// The access of an entity type as it was registered: its kind, the operations it has, the sensitive ones, the default list of a new row and,
/// for a detail, its master.
/// </summary>
public sealed class EntityAccessDescriptor
{
    internal EntityAccessDescriptor(Type entityType, EntityAccessOptions options, bool strict = true)
    {
        var own = options.Operations.Where(operation => !EntityOperations.Base.Contains(operation, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToList();
        foreach (var operation in own.Concat(options.Sensitive))
        {
            if (string.IsNullOrWhiteSpace(operation) || operation.Contains('.', StringComparison.Ordinal) || operation.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException($"'{operation}' is not a name of an operation of {entityType.Name}: a word without dots and spaces.", nameof(options));
            }
        }

        EntityType = entityType;
        Grants = options.Grants;
        Kind = KindOf(entityType, strict);
        if (Kind == EntityAccessKind.Detail)
        {
            MasterType = MasterTypeOf(entityType);
            MasterProperty = MasterPropertyOf(entityType, strict);
        }

        Operations = Kind == EntityAccessKind.Acl ? [.. EntityOperations.Base, .. own] : [.. EntityOperations.All, .. own];
        var unknown = options.Sensitive.Where(operation => !Operations.Contains(operation, StringComparer.Ordinal)).ToList();
        if (unknown.Count != 0)
        {
            throw new ArgumentException($"The sensitive operations {string.Join(", ", unknown)} are not operations of {entityType.Name}.", nameof(options));
        }

        Sensitive = new HashSet<string>(options.Sensitive, StringComparer.Ordinal);
        if (Kind != EntityAccessKind.Acl && options.DefaultAcl is not null)
        {
            throw new ArgumentException($"{entityType.Name} is a {Kind.ToString().ToLowerInvariant()} without access lists of its own: it has no default list.", nameof(options));
        }

        DefaultAcl = Kind != EntityAccessKind.Acl ? [] : options.DefaultAcl ?? [.. AclEntry.For(AclSubject.Owner, EntityOperations.OfRow(Operations).Where(operation => !Sensitive.Contains(operation)))];
        var stranger = DefaultAcl.Where(entry => !Operations.Contains(entry.Operation, StringComparer.Ordinal) || entry.Operation == EntityOperations.Create).ToList();
        if (stranger.Count != 0)
        {
            throw new ArgumentException($"The default access list of {entityType.Name} names operations a row does not have: {string.Join(", ", stranger.Select(entry => entry.Operation))}.", nameof(options));
        }
    }

    // Exactly one kind; a type of none (or of several) is not registered. A type that was never registered is read leniently
    private static EntityAccessKind KindOf(Type entityType, bool strict)
    {
        List<EntityAccessKind> kinds = [];
        if (typeof(IAclEntity).IsAssignableFrom(entityType))
        {
            kinds.Add(EntityAccessKind.Acl);
        }

        if (typeof(IAclDetail).IsAssignableFrom(entityType))
        {
            kinds.Add(EntityAccessKind.Detail);
        }

        if (typeof(ITableAccess).IsAssignableFrom(entityType))
        {
            kinds.Add(EntityAccessKind.Table);
        }

        if (kinds.Count == 1)
        {
            return kinds[0];
        }

        if (!strict)
        {
            return kinds.Count == 0 ? EntityAccessKind.Table : kinds[0];
        }

        throw new InvalidOperationException($"{entityType.Name} must be one kind of entity: IAclEntity (an access list of its own), IAclDetail<TMaster> (the list of its master) or ITableAccess (the permissions of the table alone); it is {(kinds.Count == 0 ? "none" : string.Join(" and ", kinds))}.");
    }

    private static Type MasterTypeOf(Type entityType)
    {
        return entityType.GetInterfaces().FirstOrDefault(row => row.IsGenericType && row.GetGenericTypeDefinition() == typeof(IAclDetail<>))?.GetGenericArguments()[0] ?? throw new InvalidOperationException($"{entityType.Name} is a detail without a master type: implement IAclDetail<TMaster>.");
    }

    private static System.Reflection.PropertyInfo? MasterPropertyOf(Type entityType, bool strict)
    {
        var marked = entityType.GetProperties().Where(row => row.IsDefined(typeof(AclMasterAttribute), true)).ToList();
        if (marked.Count == 1 && marked[0].PropertyType == typeof(Guid) && marked[0].GetCustomAttributes(typeof(IndexAttribute), true).OfType<IndexAttribute>().Any(row => row.Btree))
        {
            return marked[0];
        }

        if (!strict)
        {
            return marked.FirstOrDefault();
        }

        throw new InvalidOperationException($"{entityType.Name} is a detail: one Guid property, [AclMaster] and [Index], holds the key of its master.");
    }

    /// <summary>The entity type.</summary>
    public Type EntityType { get; }

    /// <summary>What the roles get by default for the table.</summary>
    public EntityGrants Grants { get; }

    /// <summary>The kind of the entity.</summary>
    public EntityAccessKind Kind { get; }

    /// <summary>Whether the rows have access lists of their own.</summary>
    public bool OwnAcl
    {
        get
        {
            return Kind == EntityAccessKind.Acl;
        }
    }

    /// <summary>The master type of a detail, else null.</summary>
    public Type? MasterType { get; }

    /// <summary>The property of a detail that holds the key of its master, else null.</summary>
    public System.Reflection.PropertyInfo? MasterProperty { get; }

    /// <summary>Every operation of the entity, the base ones first; each is an Orchard permission.</summary>
    public IReadOnlyList<string> Operations { get; }

    /// <summary>The operations that need an explicit entry even for an administrator.</summary>
    public IReadOnlySet<string> Sensitive { get; }

    /// <summary>The access list of a new row; empty for a detail and a table.</summary>
    public IReadOnlyList<AclEntry> DefaultAcl { get; }

    /// <summary>The operations a row can allow (all but <see cref="EntityOperations.Create"/>).</summary>
    public IEnumerable<string> RowOperations
    {
        get
        {
            return EntityOperations.OfRow(Operations);
        }
    }

    /// <summary>The descriptor of a type that was never registered: its kind as far as it tells, the base operations, nothing sensitive.</summary>
    public static EntityAccessDescriptor Unregistered(Type entityType)
    {
        return new EntityAccessDescriptor(entityType, new EntityAccessOptions { Grants = EntityGrants.UserData }, strict: false);
    }
}

/// <summary>
/// The access to the data: whether a role may do an operation on the table (Orchard's permissions <c>&lt;Entity&gt;.&lt;Operation&gt;</c>, granted on
/// the page of the roles) and whether the access list of the row gives the operation to one of the caller's subjects (<see cref="EntityAcl"/>). An
/// operation needs both: the matrix is the ceiling, the list is the scope.
/// </summary>
public static class EntityPermissions
{
    private static readonly ConcurrentDictionary<Type, EntityAccessDescriptor> s_descriptors = new();
    private static readonly ConcurrentDictionary<string, EntityAccessDescriptor> s_byName = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the name of the Orchard permission for an operation on an entity type. A detail is no object of the permissions: an operation on
    /// it is one on its master, reading for reading and changing for anything else (<see cref="EntityAcl.MasterOperation"/>).
    /// </summary>
    public static string Name(Type entityType, string operation)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        if (s_descriptors.TryGetValue(entityType, out var descriptor) && descriptor is { Kind: EntityAccessKind.Detail, MasterType: { } master })
        {
            return $"{master.Name}.{EntityAcl.MasterOperation(operation)}";
        }

        return $"{entityType.Name}.{operation}";
    }

    /// <summary>
    /// Records the access of an entity type, so that the storage and the authorization know its operations.
    /// </summary>
    public static EntityAccessDescriptor Register(Type entityType, EntityAccessOptions options)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(options);
        var descriptor = new EntityAccessDescriptor(entityType, options);
        s_descriptors[entityType] = descriptor;
        s_byName[entityType.Name] = descriptor;
        return descriptor;
    }

    /// <summary>
    /// Records the default grants of an entity type with the base operations.
    /// </summary>
    public static EntityAccessDescriptor Register(Type entityType, EntityGrants grants)
    {
        return Register(entityType, new EntityAccessOptions { Grants = grants });
    }

    /// <summary>
    /// Returns the access of an entity type as it was registered, or the one of an unregistered type.
    /// </summary>
    public static EntityAccessDescriptor Of(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return s_descriptors.TryGetValue(entityType, out var descriptor) ? descriptor : EntityAccessDescriptor.Unregistered(entityType);
    }

    /// <summary>
    /// Finds a registered entity by the name of its type (<c>FhirServer</c>), as its permissions and the address of its access list name it.
    /// </summary>
    /// <returns>The access of the entity, or null when no registered entity has the name.</returns>
    public static EntityAccessDescriptor? Find(string entityName)
    {
        ArgumentNullException.ThrowIfNull(entityName);
        return s_byName.TryGetValue(entityName, out var descriptor) ? descriptor : null;
    }

    /// <summary>
    /// Tells whether the type was registered.
    /// </summary>
    public static bool IsRegistered(Type entityType)
    {
        return s_descriptors.ContainsKey(entityType);
    }

    /// <summary>
    /// Finds the operation a permission names on an entity type: <c>FhirServer.Update</c> on <c>FhirServer</c> is <c>Update</c>.
    /// </summary>
    /// <returns>The operation, or null when the permission is not one of the type.</returns>
    /// <remarks>On a detail it is a permission of its master, reading or changing: <c>Package.Read</c> on <c>ConformanceResource</c> is <c>Read</c>.</remarks>
    public static string? OperationOf(Type entityType, string permission)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(permission);
        if (s_descriptors.TryGetValue(entityType, out var descriptor) && descriptor is { Kind: EntityAccessKind.Detail, MasterType: { } master })
        {
            var ofMaster = permission.StartsWith(master.Name + ".", StringComparison.Ordinal) ? permission[(master.Name.Length + 1)..] : null;
            return ofMaster is EntityOperations.Read or EntityOperations.Update ? ofMaster : null;
        }

        var prefix = entityType.Name + ".";
        if (!permission.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var operation = permission[prefix.Length..];
        return Of(entityType).Operations.Contains(operation, StringComparer.Ordinal) ? operation : null;
    }

    /// <summary>
    /// Tells whether the storage checks the operation on the table: a table of a service is checked for reading only, an unknown one (a test entity) is not checked.
    /// </summary>
    public static bool IsChecked(Type entityType, string operation)
    {
        if (!s_descriptors.TryGetValue(entityType, out var descriptor))
        {
            return false;
        }

        return operation == EntityOperations.Read || !descriptor.Grants.HasFlag(EntityGrants.ServiceWrites);
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
/// <param name="descriptor">The access of the entity.</param>
public sealed class EntityOperationSource(EntityAccessDescriptor descriptor) : IFloxOperationSource, IFloxDefaultGrantSource
{
    /// <summary>
    /// Registers the base operations of an entity type with the grants.
    /// </summary>
    public EntityOperationSource(Type entityType, EntityGrants grants) : this(new EntityAccessDescriptor(entityType, new EntityAccessOptions { Grants = grants }))
    {
    }

    /// <summary>The access of the entity.</summary>
    public EntityAccessDescriptor Descriptor
    {
        get
        {
            return descriptor;
        }
    }

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
        // A detail has no permissions of its own: its operations are those of its master
        if (descriptor.Kind == EntityAccessKind.Detail)
        {
            return [];
        }

        return [.. descriptor.Operations.Select(operation => $"{descriptor.EntityType.Name}.{operation}")];
    }

    /// <inheritdoc />
    public IReadOnlyList<DefaultGrant> GetDefaultGrants()
    {
        var entityType = descriptor.EntityType;
        var grants = descriptor.Grants;
        List<DefaultGrant> result = [];
        if (descriptor.Kind == EntityAccessKind.Detail)
        {
            return result;
        }

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
