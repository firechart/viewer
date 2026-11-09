using System.Collections.Concurrent;
using System.Text.Json;
using Flox.Core;
using Flox.Core.Host;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// The resource types a server declares.
/// </summary>
/// <param name="FhirVersion">The version the server declares (<c>4.0.1</c>); empty when the types come from the SDK.</param>
/// <param name="Release">Its release (<c>R4</c>); the version itself when it is not known.</param>
/// <param name="Types">The types in ordinal order.</param>
/// <example>
/// <code>
/// var types = await provider.GetAsync(server);
/// var hasPatients = types.Types.Contains("Patient");
/// </code>
/// </example>
public sealed record ServerResourceTypes(string FhirVersion, string Release, IReadOnlyList<string> Types);

/// <summary>
/// The resource types a server declares in its <c>CapabilityStatement</c> (<c>rest.resource</c>), as a flat list.
/// </summary>
/// <example>
/// <code>
/// var types = await provider.GetAsync(server, refresh: true);
/// </code>
/// </example>
public interface IServerResourceTypeProvider
{
    /// <summary>
    /// The types of a server. The answer is kept in memory (the lists of the server need its release too).
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="refresh">Ask the server again, as when a server is opened.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The types. When <c>/metadata</c> fails and the record says R4, the types of the SDK.</returns>
    /// <exception cref="HttpRequestException">The server did not answer, and its release is not known.</exception>
    /// <exception cref="FloxException">The answer is not a <c>CapabilityStatement</c>, and the release is not known.</exception>
    Task<ServerResourceTypes> GetAsync(IViewerServer server, bool refresh = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IServerResourceTypeProvider"/> over HTTP; a singleton (it keeps the answers).
/// </summary>
/// <param name="httpClientFactory">Makes the client <see cref="FhirServerHttp.HttpClientName"/>.</param>
/// <param name="credentials">The <c>Authorization</c> of the servers.</param>
/// <param name="logger">Logs a server without <c>/metadata</c>.</param>
/// <example>
/// <code>
/// var provider = new ServerResourceTypeProvider(httpClientFactory, credentials, loggerFactory.CreateLogger&lt;ServerResourceTypeProvider&gt;());
/// </code>
/// </example>
[FloxService(typeof(IServerResourceTypeProvider), ServiceLifetime.Singleton)]
public sealed class ServerResourceTypeProvider(IHttpClientFactory httpClientFactory, IServerCredentialStore credentials, ILogger<ServerResourceTypeProvider> logger) : IServerResourceTypeProvider
{
    /// <summary>
    /// The name of its <see cref="HttpClient"/>, the one of the FHIR servers.
    /// </summary>
    public const string HttpClientName = FhirServerHttp.HttpClientName;

    private static readonly Dictionary<string, string> s_releases = new(StringComparer.Ordinal)
    {
        ["1.0"] = "DSTU2",
        ["3.0"] = "STU3",
        ["4.0"] = "R4",
        ["4.3"] = "R4B",
        ["5.0"] = "R5",
        ["6.0"] = "R6"
    };

    private readonly ConcurrentDictionary<string, ServerResourceTypes> _known = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async Task<ServerResourceTypes> GetAsync(IViewerServer server, bool refresh = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        var key = $"{server.Key}|{server.BaseUrl}|{server.FhirRelease}";
        if (!refresh && _known.TryGetValue(key, out var known))
        {
            return known;
        }

        var result = await ReadAsync(server, cancellationToken);
        _known[key] = result;
        return result;
    }

    private async Task<ServerResourceTypes> ReadAsync(IViewerServer server, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadMetadataAsync(server, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FloxException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            if (server.FhirRelease is not { } release)
            {
                throw;
            }

            logger.LogInformation(ex, "Metadata of the server {Server} is unavailable, the types of release {Release} come from the SDK", server.Name, release);
            return FromSdk(release.GetLiteral());
        }
    }

    private async Task<ServerResourceTypes> ReadMetadataAsync(IViewerServer server, CancellationToken cancellationToken)
    {
        using var document = await FhirServerHttp.GetJsonAsync(httpClientFactory, server.BaseUrl.TrimEnd('/') + "/metadata?_summary=true", credentials.GetAuthorization(server), cancellationToken);
        return Parse(document.RootElement);
    }

    // Release-agnostic on purpose: the shape of rest.resource.type is the same from STU3 to R6
    internal static ServerResourceTypes Parse(JsonElement capabilityStatement)
    {
        if (capabilityStatement.ValueKind != JsonValueKind.Object ||
            !capabilityStatement.TryGetProperty("resourceType", out var resourceType) ||
            resourceType.GetString() != nameof(CapabilityStatement))
        {
            throw new FloxException("The server did not answer with a CapabilityStatement.");
        }

        var version = capabilityStatement.TryGetProperty("fhirVersion", out var fhirVersion) ? fhirVersion.GetString() ?? string.Empty : string.Empty;
        var types = new SortedSet<string>(StringComparer.Ordinal);
        if (capabilityStatement.TryGetProperty("rest", out var rest) && rest.ValueKind == JsonValueKind.Array)
        {
            foreach (var restEntry in rest.EnumerateArray())
            {
                if (!restEntry.TryGetProperty("resource", out var resources) || resources.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var resource in resources.EnumerateArray())
                {
                    if (resource.TryGetProperty("type", out var type) && type.GetString() is { Length: > 0 } name)
                    {
                        types.Add(name);
                    }
                }
            }
        }

        return new ServerResourceTypes(version, ReleaseOf(version), [.. types]);
    }

    // By major.minor: servers declare 4.0.1 and 4.0.0 for R4
    internal static string ReleaseOf(string fhirVersion)
    {
        var parts = fhirVersion.Split('.');
        return parts.Length >= 2 && s_releases.TryGetValue($"{parts[0]}.{parts[1]}", out var release) ? release : fhirVersion;
    }

    // Only the SDK that the application carries: R4
    private static ServerResourceTypes FromSdk(string release)
    {
        if (!string.Equals(release, "R4", StringComparison.OrdinalIgnoreCase))
        {
            throw new FloxException($"The types of release {release} are not known to this application.");
        }

        return new ServerResourceTypes(string.Empty, "R4", [.. ModelInfo.SupportedResources.Order(StringComparer.Ordinal)]);
    }
}
