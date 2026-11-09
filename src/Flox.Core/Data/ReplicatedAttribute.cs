namespace Flox.Core.Data;

public enum ConflictPolicy
{
    ServerWins = 0,
    LastWriteWins = 1
}

public enum ReplicationDirection
{
    Both = 0,
    PullOnly = 1
}

public enum ReplicationScope
{
    // The documents of the caller
    Own = 0,

    // The documents of the caller and those that everyone may read (ReadAccess = Everyone), whoever owns them
    OwnAndShared = 1
}

// The type is replicated between the server and the clients. Name is the contract on the wire: never rename
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ReplicatedAttribute : Attribute
{
    public ReplicatedAttribute() { }

    public ReplicatedAttribute(string name)
    {
        Name = name;
    }

    public string? Name { get; }

    public ConflictPolicy Conflicts { get; set; } = ConflictPolicy.ServerWins;

    public ReplicationDirection Direction { get; set; }

    public ReplicationScope Scope { get; set; }
}
