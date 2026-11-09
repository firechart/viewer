namespace Flox.Core.Data;

// What the server keeps beside a document: the store metadata mapped onto the entity (only for [Replicated] types), never sent to clients
public interface IReplicaState
{
    long Revision { get; set; }
    DateTimeOffset ModifiedAt { get; set; }
    bool Deleted { get; set; }
}
