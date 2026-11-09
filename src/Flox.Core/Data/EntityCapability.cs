namespace Flox.Core.Data;

// What the caller may do with one document: the permission of the table and the rights of the row together. The server answers it, so that a client does
// not repeat the rules (it does not even know the owner or the roles of the document)
public sealed record EntityCapability<TKey>
(
    TKey Key,
    bool CanRead,
    bool CanWrite,
    bool CanDelete
);

// The answer of the Capabilities action of the OData controllers
public sealed record EntityCapabilitiesResponse
(
    List<EntityCapability<Guid>> Value
);
