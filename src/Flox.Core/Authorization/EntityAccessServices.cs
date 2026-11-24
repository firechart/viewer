using System.Security.Claims;
using Flox.Core.Data;

namespace Flox.Core.Authorization;

/// <summary>
/// Reads the access lists of the rows, kept apart from the documents; the server keeps them in a table of its database.
/// </summary>
public interface IEntityAclStore
{
    /// <summary>Returns the access list of one row; empty when it has none.</summary>
    /// <param name="entityType">The entity type of the row.</param>
    /// <param name="key">The key of the row (<see cref="EntityAcl.KeyOf"/>).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<IReadOnlyList<AclEntry>> GetAsync(Type entityType, string key, CancellationToken cancellationToken = default);

    /// <summary>Returns the access lists of several rows of one type, by key; a row without a list is missing from the answer.</summary>
    /// <param name="entityType">The entity type of the rows.</param>
    /// <param name="keys">The keys of the rows.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<AclEntry>>> GetManyAsync(Type entityType, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>Returns the master of a detail, or null when it does not exist.</summary>
    /// <param name="detail">The detail.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<IAclEntity?> GetMasterAsync(IAclDetail detail, CancellationToken cancellationToken = default);
}

/// <summary>
/// A row as the resource of an authorization, with what the caller already knows of it. An <see cref="IEntity"/> given as the resource is the
/// same as a probe that is not quiet and has no access list.
/// </summary>
/// <param name="Entity">The row.</param>
/// <param name="Acl">The access list of the row (of the master of a detail) when it is already loaded (a list of rows reads them at once); null
/// reads it.</param>
/// <param name="Quiet">True when the caller only asks (what may be done with a row, for a screen): a refusal is not an attempt, so it is not
/// recorded in the audit trail.</param>
/// <param name="Master">The master of a detail when it is already loaded; null reads it.</param>
public sealed record EntityProbe(IEntity Entity, IReadOnlyList<AclEntry>? Acl = null, bool Quiet = true, IAclEntity? Master = null);

/// <summary>A change of the access list of a row.</summary>
/// <param name="EntityType">The entity type of the row.</param>
/// <param name="Key">The key of the row.</param>
/// <param name="Before">The list before the change; empty for a new row.</param>
/// <param name="After">The list after the change.</param>
/// <param name="ChangedBy">Who changed it.</param>
public sealed record EntityAccessChange(Type EntityType, string Key, IReadOnlyList<AclEntry> Before, IReadOnlyList<AclEntry> After, ClaimsPrincipal ChangedBy)
{
    /// <summary>The entries the change added.</summary>
    public IReadOnlyList<AclEntry> Added
    {
        get
        {
            return [.. After.Except(Before)];
        }
    }

    /// <summary>The entries the change removed.</summary>
    public IReadOnlyList<AclEntry> Removed
    {
        get
        {
            return [.. Before.Except(After)];
        }
    }

    /// <summary>The added entries of sensitive operations.</summary>
    public IReadOnlyList<AclEntry> SensitiveAdded
    {
        get
        {
            var sensitive = EntityPermissions.Of(EntityType).Sensitive;
            return [.. Added.Where(entry => sensitive.Contains(entry.Operation))];
        }
    }

    /// <summary>The added entries of sensitive operations that the author gave to themselves (by user, by owner, by a role they hold, or to all).</summary>
    public IReadOnlyList<AclEntry> SensitiveSelfGranted(string ownerId)
    {
        return [.. SensitiveAdded.Where(entry => EntityAcl.Matches(entry.Subject, ownerId, ChangedBy))];
    }
}

/// <summary>
/// Hears the changes of the access lists of rows, before they are saved: the server records them in the audit trail. A listener that throws
/// refuses the change.
/// </summary>
public interface IEntityAccessListener
{
    /// <summary>Called before the access list of a row is changed or a row is created with a list of its writer.</summary>
    /// <param name="change">The change.</param>
    /// <param name="ownerId">The owner of the row.</param>
    /// <param name="cancellationToken">Cancels the work of the listener.</param>
    Task AccessChangedAsync(EntityAccessChange change, string ownerId, CancellationToken cancellationToken = default);
}
