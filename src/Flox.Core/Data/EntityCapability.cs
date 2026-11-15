namespace Flox.Core.Data;

/// <summary>
/// What the caller may do with one document: the permission of the table and the rights of the row together. The server answers it, so that a client does
/// not repeat the rules (it does not even know the owner or the roles of the document).
/// </summary>
/// <param name="Key">The key of the document.</param>
/// <param name="CanRead">The caller may read the document.</param>
/// <param name="CanWrite">The caller may change the document.</param>
/// <param name="CanDelete">The caller may delete the document.</param>
public sealed record EntityCapability<TKey>
(
    TKey Key,
    bool CanRead,
    bool CanWrite,
    bool CanDelete
);

/// <summary>
/// The answer of the Capabilities action of the OData controllers.
/// </summary>
/// <param name="Value">The capabilities, one per requested document.</param>
public sealed record EntityCapabilitiesResponse
(
    List<EntityCapability<Guid>> Value
);
