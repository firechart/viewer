using System.Security.Cryptography;
using System.Text;
using Flox.Core.Data;

namespace Flox.Core.Models;

/// <summary>A bookmark of the user, kept on the device only (it may name a patient): never sent to a server, no roaming, moved between devices by an export. It holds enough to open its place again in any host: who opens it (<see cref="Type"/>), where (the <c>Source*</c> properties: the FHIR server, its address outliving the host) and what (<see cref="EntityId"/> and <see cref="Location"/>). A search is a place too.</summary>
public class UserBookmark : GuidEntity, IEntityDisplayInfo
{
    /// <summary>The kind of the place (a resource type, a package), shown under the title.</summary>
    [Index]
    public string Category { get; set; } = default!;

    /// <summary>The text shown for the bookmark.</summary>
    [Index(Like = true, FullText = FullTextWeight.A)]
    public string DisplayName { get; set; } = default!;

    /// <summary>The address in the application when the bookmark was made; a host that knows <see cref="Type"/> builds a fresh one from the source and the location.</summary>
    [Index]
    public string Url { get; set; } = default!;

    /// <summary>Who opens the bookmark: the full name of a bookmarked entity type, or a kind of place of its own (<see cref="UserBookmarkTypes"/>).</summary>
    [Index]
    public string Type { get; set; } = default!;

    /// <summary>The identity of the place inside its source: the id of an entity, such as <c>Patient/123</c>, or the location of a search.</summary>
    [Index]
    public string EntityId { get; set; } = default!;

    /// <summary>The source as the host that made the bookmark knows it (the id of a registered FHIR server); a hint only, since another host finds the source by <see cref="SourceAddress"/>.</summary>
    [Index]
    public string? SourceKey { get; set; }

    /// <summary>The name of the source.</summary>
    [Index(Like = true)]
    public string? SourceName { get; set; }

    /// <summary>The address of the source that is the same in every host (the base URL of a FHIR server); none for a place of the application itself.</summary>
    [Index]
    public string? SourceAddress { get; set; }

    /// <summary>The address inside the source, put by the host below its own route of the source, such as <c>resources/Patient/123</c>.</summary>
    public string? Location { get; set; }

    /// <summary>The icon of the bookmark.</summary>
    public string? Icon { get; set; }

    /// <summary>The id of the bookmark of one place, so adding it twice (on one device, or by an import) is the same document.</summary>
    /// <param name="type">The kind of the bookmark.</param>
    /// <param name="sourceAddress">The address of the source.</param>
    /// <param name="entityId">The identity of the place inside the source.</param>
    /// <returns>A stable id derived from the three values.</returns>
    public static Guid KeyOf(string type, string? sourceAddress, string entityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{type}|{NormalizeAddress(sourceAddress)}|{entityId}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>Trims an address and removes its trailing slash.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The normalized address, or null when it is empty.</returns>
    public static string? NormalizeAddress(string? address)
    {
        return string.IsNullOrWhiteSpace(address) ? null : address.Trim().TrimEnd('/');
    }
}

/// <summary>The kinds of places a bookmark can point to, besides entity types.</summary>
public static class UserBookmarkTypes
{
    /// <summary>A place of the FHIR Viewer: a page of a FHIR server (<see cref="UserBookmark.SourceAddress"/> is its base URL, <see cref="UserBookmark.Location"/> the address in the Viewer).</summary>
    public const string Viewer = "fhir-viewer";
}
