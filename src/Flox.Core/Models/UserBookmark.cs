using System.Security.Cryptography;
using System.Text;
using Flox.Core.Data;

namespace Flox.Core.Models;

// A bookmark of the user, kept on the device only (it may name a patient): never sent to a server, no roaming; moved between devices by an export.
// It holds enough to open its place again in any host: who opens it (Type), where (the Source*: the FHIR server, its address outliving the host)
// and what (EntityId, Location). A search is a place too (Location with a query)
public class UserBookmark : GuidEntity, IEntityDisplayInfo
{
    // Shown under the title: the kind of the place (a resource type, a package)
    [Index]
    public string Category { get; set; } = default!;

    [Index(Like = true, FullText = FullTextWeight.A)]
    public string DisplayName { get; set; } = default!;

    // The address in the application when the bookmark was made; a host that knows Type builds a fresh one from the Source and the Location
    [Index]
    public string Url { get; set; } = default!;

    // Who opens it: the full name of a bookmarked entity type, or a kind of place of its own (UserBookmarkTypes)
    [Index]
    public string Type { get; set; } = default!;

    // The identity of the place inside its source (the id of an entity; "Patient/123", or the location of a search)
    [Index]
    public string EntityId { get; set; } = default!;

    // The source as the host that made the bookmark knows it (the id of a registered FHIR server, a server of a configuration); a hint only:
    // another host or a server registered again finds the source by SourceAddress
    [Index]
    public string? SourceKey { get; set; }

    [Index(Like = true)]
    public string? SourceName { get; set; }

    // The address of the source that is the same in every host (the base URL of a FHIR server); none for a place of the application itself
    [Index]
    public string? SourceAddress { get; set; }

    // The address inside the source, put by the host below its own route of the source ("resources/Patient/123", "summary/123")
    public string? Location { get; set; }

    public string? Icon { get; set; }

    // One bookmark of one place: adding it twice (on one device, or by an import) is the same document
    public static Guid KeyOf(string type, string? sourceAddress, string entityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{type}|{NormalizeAddress(sourceAddress)}|{entityId}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static string? NormalizeAddress(string? address)
    {
        return string.IsNullOrWhiteSpace(address) ? null : address.Trim().TrimEnd('/');
    }
}

public static class UserBookmarkTypes
{
    // A place of the FHIR Viewer: a page of a FHIR server (SourceAddress is its base URL, Location the address of the Viewer)
    public const string Viewer = "fhir-viewer";
}
