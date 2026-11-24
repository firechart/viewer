using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Security.Claims;
using Flox.Core.Data;

namespace Flox.Core.Authorization;

/// <summary>
/// The rule of the access lists of the rows, one for the whole platform: an operation on a row is allowed when the role of the caller has the
/// permission <c>&lt;Entity&gt;.&lt;Operation&gt;</c> (the matrix, checked by Orchard) and the access list of the row gives the operation to one
/// of the caller's subjects (this class). The administrator passes the list for every operation that is not sensitive.
/// </summary>
public static class EntityAcl
{
    private static readonly ConcurrentDictionary<Type, Func<IEntity, string>> s_keys = new();

    /// <summary>
    /// The keys of the subjects of the caller: <c>everyone</c>, and for a signed-in caller <c>authenticated</c>, <c>user:ID</c> and a
    /// <c>role:NAME</c> per role. The owner is not among them: it is matched against the owner of the row.
    /// </summary>
    public static IReadOnlyList<string> SubjectKeysOf(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.IsAuthenticated())
        {
            return [AclSubject.Everyone.Key];
        }

        return [AclSubject.Everyone.Key, AclSubject.Authenticated.Key, AclSubject.User(user.GetUserId()).Key, .. user.GetRoles().Select(role => AclSubject.Role(role).Key).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Tells whether one entry of an access list is for the caller.
    /// </summary>
    /// <param name="subject">The subject of the entry.</param>
    /// <param name="ownerId">The owner of the row.</param>
    /// <param name="user">The caller.</param>
    public static bool Matches(AclSubject subject, string ownerId, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (subject.Kind == AclSubjectKind.Everyone)
        {
            return true;
        }

        if (!user.IsAuthenticated())
        {
            return false;
        }

        return subject.Kind switch
        {
            AclSubjectKind.Authenticated => true,
            AclSubjectKind.Owner => string.Equals(ownerId, user.GetUserId(), StringComparison.Ordinal),
            AclSubjectKind.User => string.Equals(subject.Name, user.GetUserId(), StringComparison.Ordinal),
            AclSubjectKind.Role => user.GetRoles().Any(role => string.Equals(AclSubject.Role(role).Name, subject.Name, StringComparison.Ordinal)),
            _ => false
        };
    }

    /// <summary>
    /// Tells whether the access list gives the operation to one of the subjects of the caller.
    /// </summary>
    /// <param name="acl">The access list of the row.</param>
    /// <param name="ownerId">The owner of the row.</param>
    /// <param name="user">The caller.</param>
    /// <param name="operation">The operation.</param>
    public static bool Allows(IEnumerable<AclEntry> acl, string ownerId, ClaimsPrincipal user, string operation)
    {
        ArgumentNullException.ThrowIfNull(acl);
        return acl.Any(entry => entry.Operation == operation && Matches(entry.Subject, ownerId, user));
    }

    /// <summary>
    /// The decision of the access list of a row, without the matrix. A table without lists allows everything; an administrator passes the
    /// list for an operation that is not sensitive; anyone else needs an entry, and for a sensitive operation an entry of a user or a role. For a detail the list and the owner are those of its master,
    /// and the operation asked of the master is <see cref="MasterOperation"/>.
    /// </summary>
    /// <param name="descriptor">The access of the entity.</param>
    /// <param name="acl">The access list of the row, or of the master of a detail (ignored for a table).</param>
    /// <param name="ownerId">The owner of the row, or of the master of a detail.</param>
    /// <param name="user">The caller.</param>
    /// <param name="operation">The operation on the row.</param>
    public static bool RowAllows(EntityAccessDescriptor descriptor, IEnumerable<AclEntry> acl, string ownerId, ClaimsPrincipal user, string operation)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(user);
        if (descriptor.Kind == EntityAccessKind.Table)
        {
            return true;
        }

        if (user.IsAdmin() && !descriptor.Sensitive.Contains(operation))
        {
            return true;
        }

        // A sensitive operation counts only the entries of a user or a role by name: an entry for the owner, everyone or the signed-in users is
        // refused when a list is saved, and one stored before the operation became sensitive gives nothing either
        if (descriptor.Sensitive.Contains(operation))
        {
            acl = acl.Where(entry => entry.Subject.Kind is AclSubjectKind.User or AclSubjectKind.Role);
        }

        return Allows(acl, ownerId, user, descriptor.Kind == EntityAccessKind.Detail ? MasterOperation(operation) : operation);
    }

    /// <summary>
    /// The operation the access list of the master must give for an operation on a detail: reading a detail is reading the master, any other
    /// operation (a change, a delete, an operation of the detail) is changing the master.
    /// </summary>
    public static string MasterOperation(string operation)
    {
        return operation == EntityOperations.Read ? EntityOperations.Read : EntityOperations.Update;
    }

    /// <summary>
    /// The key of a row as the table of the access lists keeps it: the text of its <c>Uid</c> (a Guid in the "D" format, lower case).
    /// </summary>
    public static string KeyOf(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return s_keys.GetOrAdd(entity.GetType(), CompileKey)(entity);
    }

    /// <summary>
    /// The key of a row as the table of the access lists keeps it.
    /// </summary>
    public static string KeyText<TKey>(TKey key) where TKey : notnull
    {
        return key switch
        {
            Guid guid => guid.ToString("D"),
            _ => key.ToString() ?? string.Empty
        };
    }

    private static Func<IEntity, string> CompileKey(Type type)
    {
        var contract = type.GetInterfaces().FirstOrDefault(row => row.IsGenericType && row.GetGenericTypeDefinition() == typeof(IEntity<>)) ?? throw new ArgumentException($"{type.Name} has no key: it is not an IEntity<TKey>.");
        var parameter = Expression.Parameter(typeof(IEntity), "entity");
        var key = Expression.Property(Expression.Convert(parameter, contract), nameof(IEntity<Guid>.Uid));
        var text = typeof(EntityAcl).GetMethod(nameof(KeyText))!.MakeGenericMethod(key.Type);
        return Expression.Lambda<Func<IEntity, string>>(Expression.Call(text, key), parameter).Compile();
    }

    /// <summary>
    /// The access list of a row of the organization (a shared FHIR server, a system setting): everyone reads it, the administrators change and
    /// delete it, its owner reads it.
    /// </summary>
    public static IReadOnlyList<AclEntry> Organization { get; } = FromLegacy("Public", null, "Role", AppRoles.ADMINISTRATOR);

    /// <summary>
    /// The access lists that the old rights of a row gave (the fields <c>ReadAccess</c>, <c>WriteAccess</c>, <c>ReadRole</c> and <c>WriteRole</c> of
    /// the documents before the access lists), without losses: the owner always reads; Public is Everyone:Read, Authenticated is
    /// Authenticated:Read, a role X is Role(X):Read; a write level gives Update and Delete to its subject, the write level Owner gives them and
    /// ManageAccess to the owner (as a new row has them). Writing needed reading before as it does now. The levels are 0 Owner, 1 Public,
    /// 2 Role, 3 Authenticated, or their names.
    /// </summary>
    /// <param name="readAccess">The old read level.</param>
    /// <param name="readRole">The old read role.</param>
    /// <param name="writeAccess">The old write level.</param>
    /// <param name="writeRole">The old write role.</param>
    public static IReadOnlyList<AclEntry> FromLegacy(string? readAccess, string? readRole, string? writeAccess, string? writeRole)
    {
        List<AclEntry> result = [new AclEntry(AclSubject.Owner, EntityOperations.Read)];
        var reader = LegacySubject(readAccess, readRole, writing: false);
        if (reader is { } readers)
        {
            result.Add(new AclEntry(readers, EntityOperations.Read));
        }

        var writer = LegacySubject(writeAccess, writeRole, writing: true);
        if (writer is { } writers)
        {
            result.AddRange(AclEntry.For(writers, EntityOperations.Update, EntityOperations.Delete));
        }
        else if (IsOwnerLevel(writeAccess))
        {
            result.AddRange(AclEntry.For(AclSubject.Owner, EntityOperations.Update, EntityOperations.Delete, EntityOperations.ManageAccess));
        }

        return [.. result.Distinct()];
    }

    private static bool IsOwnerLevel(string? level)
    {
        return level?.Trim() is null or "" or "0" or "Owner";
    }

    // null for the owner; a write level of Public was never allowed, a Role without a role gives nothing
    private static AclSubject? LegacySubject(string? level, string? role, bool writing)
    {
        return level?.Trim() switch
        {
            null or "" or "0" or "Owner" => null,
            "1" or "Public" => writing ? null : AclSubject.Everyone,
            "2" or "Role" => string.IsNullOrWhiteSpace(role) ? null : AclSubject.Role(role),
            "3" or "Authenticated" => AclSubject.Authenticated,
            _ => null
        };
    }
}
