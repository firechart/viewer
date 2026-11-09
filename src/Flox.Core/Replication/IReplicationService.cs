using Flox.Core.Data;

namespace Flox.Core.Replication;

// The client side of replication, for whoever starts it: a host component, a button, a timer. The documents of the signed-in user only
public interface IReplicationService
{
    // Documents that came from the server and are now in the local store
    event EventHandler<ReplicationAppliedEventArgs>? Applied;

    // Pushes the local changes and pulls the server's, now. Does nothing for a user who is not signed in
    Task SyncAsync(CancellationToken cancellationToken = default);

    // The same for one replicated type (its wire name, see ReplicatedAttribute); an unknown name does nothing
    Task SyncAsync(string type, CancellationToken cancellationToken = default);

    // Syncs one type every interval (not less than a second) until the returned handle is disposed: for a screen that shows data
    // the server changes by itself, such as a running job. A tick is skipped while the previous sync is on its way
    IDisposable StartPolling(string type, TimeSpan interval);

    // The same a little later, once for a burst of requests; errors go to the log
    void RequestSync();

    // The server refused a change just now (once per change, also for one that failed in an earlier session and was not announced yet)
    event EventHandler<ReplicationRejectedEventArgs>? Rejected;

    // Something changed that a status screen shows: a sync began or ended, a change is waiting to be sent
    event EventHandler? StateChanged;

    Task<ReplicationStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    // The changes the server refused: they are not sent again until RetryFailedAsync
    Task<IReadOnlyList<ReplicationFailure>> GetFailuresAsync(CancellationToken cancellationToken = default);

    // Marks the refused changes to be sent once more, and syncs
    Task RetryFailedAsync(CancellationToken cancellationToken = default);
}

public sealed record ReplicationTypeStatus
(
    string Type,
    ReplicationDirection Direction,
    ConflictPolicy Conflicts,
    int Pending,
    int Failed,
    DateTimeOffset? LastSyncedAt,
    string? Cursor
);

public sealed record ReplicationStatus
(
    bool SignedIn,
    bool IsSyncing,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset? LastAttemptAt,
    string? LastError,
    IReadOnlyList<ReplicationTypeStatus> Types
)
{
    public int Pending => Types.Sum(row => row.Pending);

    public int Failed => Types.Sum(row => row.Failed);
}

// A refused change: the code of the reason and, when the server told, what is wrong with which field
public sealed record ReplicationFailure
(
    string Type,
    string DocumentId,
    string? Reason,
    DateTimeOffset EditedAt,
    IReadOnlyList<ReplicaError>? Errors = null
);

public static class ReplicationServiceExtensions
{
    public static Task SyncAsync<TEntity>(this IReplicationService service, CancellationToken cancellationToken = default)
    {
        return service.SyncAsync(ReplicatedType.Of(typeof(TEntity)).Name, cancellationToken);
    }

    public static IDisposable StartPolling<TEntity>(this IReplicationService service, TimeSpan interval)
    {
        return service.StartPolling(ReplicatedType.Of(typeof(TEntity)).Name, interval);
    }
}

public sealed class ReplicationRejectedEventArgs
(
    ReplicationFailure failure
) : EventArgs
{
    public ReplicationFailure Failure { get; } = failure;
}

public sealed class ReplicationAppliedEventArgs
(
    string type,
    IReadOnlyList<object> documents
) : EventArgs
{
    public string Type { get; } = type;

    // The entities as stored; a deleted document is not among them
    public IReadOnlyList<object> Documents { get; } = documents;
}

// How the client reaches /api/replication; null when the server does not replicate the type
public interface IReplicaTransport
{
    Task<ReplicaPullResponse?> PullAsync(string type, string? cursor, int limit, CancellationToken cancellationToken = default);

    Task<ReplicaPushResponse?> PushAsync(string type, ReplicaPushRequest request, CancellationToken cancellationToken = default);
}
