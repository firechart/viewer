using System.Text.Json;
using Flox.Core.Data;

namespace Flox.Core.Replication;

// The wire of /api/replication. A document travels as JSON without the server-managed fields; the state of replication is in the envelope

public enum ReplicaPushStatus
{
    // The change is applied (or there was nothing to apply); Revision is the new one
    Accepted = 0,

    // Another change got there first; Current is what the server has
    Conflict = 1,

    // Not allowed or not valid; nothing is told about documents of others
    Rejected = 2
}

public sealed class ReplicaPulledItem
{
    public string Id { get; set; } = string.Empty;

    public long Revision { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }

    // When the user made the last change (the clamped time the client reported): the same on every device, unlike the time of the sync
    public DateTimeOffset EditedAt { get; set; }

    public bool Deleted { get; set; }

    // Null for a deleted document
    public JsonElement? Doc { get; set; }
}

public sealed class ReplicaPullResponse
{
    public List<ReplicaPulledItem> Items { get; init; } = [];

    // Opaque: the next pull starts from it
    public string? Cursor { get; set; }

    public bool HasMore { get; set; }
}

public sealed class ReplicaPushItem
{
    public string Id { get; set; } = string.Empty;

    // The revision the client saw last; 0 for a document it created
    public long BaseRevision { get; set; }

    // When the user made the change; the server never takes it beyond its own now
    public DateTimeOffset UpdatedAt { get; set; }

    public bool Deleted { get; set; }

    public JsonElement? Doc { get; set; }
}

public sealed class ReplicaPushRequest
{
    public List<ReplicaPushItem> Items { get; init; } = [];
}

// What is wrong with a refused document, for the person who made the change: the field (a property name; null for the document as a whole) and the text
public sealed class ReplicaError
{
    public string? Field { get; set; }

    public string Message { get; set; } = string.Empty;
}

public sealed class ReplicaPushResult
{
    public string Id { get; set; } = string.Empty;

    public ReplicaPushStatus Status { get; set; }

    public long Revision { get; set; }

    public ReplicaPulledItem? Current { get; set; }

    // A short code of why the change was refused (invalid, forbidden, read-only, conflict)
    public string? Reason { get; set; }

    // The details of a refusal, when the server has them (the failed rules of a validator)
    public List<ReplicaError> Errors { get; init; } = [];
}

public sealed class ReplicaPushResponse
{
    public List<ReplicaPushResult> Results { get; init; } = [];
}

public sealed class ReplicaTypeInfo
{
    public string Name { get; set; } = string.Empty;

    public ConflictPolicy Conflicts { get; set; }

    public ReplicationDirection Direction { get; set; }
}
