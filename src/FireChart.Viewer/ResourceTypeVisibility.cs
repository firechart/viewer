using System.Security.Cryptography;
using System.Text;
using Flox.Core.Data;
using Flox.Core.Models;

namespace FireChart.Viewer;

/// <summary>
/// Which resource types the first page of a server (the list of its types) shows: one document per server, kept on the device by the host (an
/// <c>IGuidEntityStorage&lt;ResourceTypeVisibility&gt;</c>, <c>AddLocalEntity&lt;ResourceTypeVisibility&gt;()</c>), never sent to a server. A host
/// without it shows every type and no command to choose them.
/// </summary>
/// <remarks>The document of a server is found by its address (<see cref="KeyOf"/>), so it outlives the registration of the server in the host.</remarks>
/// <example>
/// <code>
/// services.AddBrowserSqlite("my-app").AddSqliteDocuments().AddLocalEntity&lt;ResourceTypeVisibility&gt;();
/// </code>
/// </example>
public class ResourceTypeVisibility : GuidEntity
{
    /// <summary>The base address of the server, normalized (<see cref="UserBookmark.NormalizeAddress"/>).</summary>
    public string ServerAddress { get; set; } = string.Empty;

    /// <summary>The key of the server in the host that saved the document (<c>IViewerServer.Key</c>); a hint only.</summary>
    public string? ServerKey { get; set; }

    /// <summary>The types chosen on the page, each with whether it is hidden; a type not named is shown.</summary>
    public IReadOnlyList<ResourceTypeVisibilityEntry> Types { get; set; } = [];

    /// <summary>
    /// The id of the document of a server.
    /// </summary>
    /// <param name="serverAddress">The base address of the server; a trailing slash does not matter.</param>
    /// <returns>A stable id derived from the address.</returns>
    /// <exception cref="ArgumentException">The address is empty.</exception>
    /// <example>
    /// <code>
    /// var key = ResourceTypeVisibility.KeyOf("https://hapi.fhir.org/baseR4/");
    /// </code>
    /// </example>
    public static Guid KeyOf(string serverAddress)
    {
        var address = UserBookmark.NormalizeAddress(serverAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(address, nameof(serverAddress));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"resource-types|{address.ToUpperInvariant()}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}

/// <summary>
/// A resource type of a <see cref="ResourceTypeVisibility"/> and whether it is hidden.
/// </summary>
/// <example>
/// <code>
/// var entry = new ResourceTypeVisibilityEntry { ResourceType = "Account", Hidden = true };
/// </code>
/// </example>
public class ResourceTypeVisibilityEntry
{
    /// <summary>The resource type (<c>Patient</c>).</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>The type is not shown on the page of the types.</summary>
    public bool Hidden { get; set; }
}
