using Flox.Core.Data;

namespace Flox.Core.Replication;

// Where a pull continues: modified time, then key, as the store orders them
public sealed record ReplicaCursor<TKey>(DateTimeOffset At, TKey Id) where TKey : notnull;

// Entities carry Revision, ModifiedAt and Deleted (IReplicaState); a deleted one is a tombstone
public sealed record ReplicaPage<TKey, TEntity>(IReadOnlyList<TEntity> Items, ReplicaCursor<TKey>? Next, bool HasMore) where TKey : notnull;

public sealed record ReplicaChange<TKey, TEntity>(TKey Id, long BaseRevision, DateTimeOffset UpdatedAt, bool Deleted, TEntity? Doc) where TKey : notnull;

public sealed record ReplicaPushOutcome<TEntity>(ReplicaPushStatus Status, long Revision, TEntity? Current, string? Reason, IReadOnlyList<ReplicaError>? Errors = null);

// The server side of replication of one [Replicated] type. Only the documents the caller owns are replicated, whatever the caller may
// otherwise read: the replica of an administrator is the administrator's own data
public interface IReplicaStorage<TKey, TEntity> where TKey : notnull where TEntity : IEntity<TKey>
{
    Task<ReplicaPage<TKey, TEntity>> PullAsync(ReplicaCursor<TKey>? cursor, int limit, CancellationToken cancellationToken = default);

    Task<ReplicaPushOutcome<TEntity>> PushAsync(ReplicaChange<TKey, TEntity> change, CancellationToken cancellationToken = default);
}
