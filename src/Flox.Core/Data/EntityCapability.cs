using System.Text.Json.Serialization;
using Flox.Core.Authorization;

namespace Flox.Core.Data;

/// <summary>
/// What the caller may do with one document: the operations the permissions of the table and the access list of the row allow together. The
/// server answers it, so that a client does not repeat the rules (it knows neither the owner nor the access list of the document).
/// </summary>
/// <param name="Key">The key of the document.</param>
/// <param name="Operations">The operations allowed (<see cref="EntityOperations"/> and those of the entity); empty for a document the caller may not read.</param>
public sealed record EntityCapability<TKey>
(
    TKey Key,
    IReadOnlyList<string> Operations
)
{
    /// <summary>Tells whether the operation is allowed.</summary>
    public bool Allows(string operation)
    {
        return Operations.Contains(operation, StringComparer.Ordinal);
    }

    /// <summary>The caller may read the document.</summary>
    [JsonIgnore]
    public bool CanRead
    {
        get
        {
            return Allows(EntityOperations.Read);
        }
    }

    /// <summary>The caller may change the document.</summary>
    [JsonIgnore]
    public bool CanUpdate
    {
        get
        {
            return Allows(EntityOperations.Update);
        }
    }

    /// <summary>The caller may delete the document.</summary>
    [JsonIgnore]
    public bool CanDelete
    {
        get
        {
            return Allows(EntityOperations.Delete);
        }
    }

    /// <summary>A document the caller may do nothing with.</summary>
    public static EntityCapability<TKey> None(TKey key)
    {
        return new EntityCapability<TKey>(key, []);
    }
}

/// <summary>
/// The answer of the Capabilities action of the OData controllers.
/// </summary>
/// <param name="Value">The capabilities, one per requested document.</param>
public sealed record EntityCapabilitiesResponse
(
    List<EntityCapability<Guid>> Value
);

/// <summary>
/// The access list of one document, as <c>api/acl/{entity}/{key}</c> reads and replaces it.
/// </summary>
/// <param name="Entries">The entries of the list.</param>
public sealed record EntityAccessList
(
    List<AclEntry> Entries
);
