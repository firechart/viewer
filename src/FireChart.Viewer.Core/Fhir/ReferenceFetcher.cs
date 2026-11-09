using System.Collections.Concurrent;
using System.Net;
using FireChart.Viewer.Core.Search;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// What a second request found for the references a rendering did not find.
/// </summary>
/// <param name="Found">The resources the server returned.</param>
/// <param name="Missing">The <c>Type/id</c> of the resources the server says do not exist (<c>404</c>, <c>410</c>).</param>
/// <example>
/// <code>
/// var fetched = await fetcher.FetchAsync(server, ["Patient/1", "Encounter/7"]);
/// var absent = fetched.Missing.Contains("Patient/1");
/// </code>
/// </example>
public sealed record FetchedReferences(IReadOnlyList<RelatedResource> Found, IReadOnlyCollection<string> Missing)
{
    /// <summary>
    /// Nothing found, nothing missing.
    /// </summary>
    public static FetchedReferences None { get; } = new([], []);
}

/// <summary>
/// Fetches the patients and the encounters a page refers to but did not get with its records (no <c>_include</c>, a block not loaded yet, a server without
/// referential integrity).
/// </summary>
/// <remarks>
/// One search by <c>_id</c> per type; what it does not return is read by its id, and only an answer <c>404</c> or <c>410</c> makes it missing: an error, a
/// refusal or a search that hides it (the rights of SMART) proves nothing. Only <c>Patient</c> and <c>Encounter</c> are fetched. The answers are kept for the
/// scope (the session of a page).
/// </remarks>
/// <example>
/// <code>
/// var fetched = await fetcher.FetchAsync(server, rendering.MissingReferences, cancellationToken);
/// </code>
/// </example>
public interface IReferenceFetcher
{
    /// <summary>
    /// Fetches the resources of references.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="keys">The references as <c>Type/id</c>; other types than <c>Patient</c> and <c>Encounter</c> are ignored.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>What was found and what the server says is missing; an error of the server leaves a key in neither.</returns>
    Task<FetchedReferences> FetchAsync(IViewerServer server, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IReferenceFetcher"/> over an <see cref="IFhirServerReader"/>; scoped (its answers live as long as a page).
/// </summary>
/// <param name="reader">Reads the server.</param>
/// <param name="logger">Logs the errors of the server (without data).</param>
/// <example>
/// <code>
/// var fetcher = new ReferenceFetcher(reader, loggerFactory.CreateLogger&lt;ReferenceFetcher&gt;());
/// </code>
/// </example>
[FloxService(typeof(IReferenceFetcher), ServiceLifetime.Scoped)]
public sealed class ReferenceFetcher(IFhirServerReader reader, ILogger<ReferenceFetcher> logger) : IReferenceFetcher
{
    // The types whose absence the rows say; the others keep what the reference says
    private static readonly HashSet<string> s_types = new(StringComparer.Ordinal) { "Patient", "Encounter" };

    private const int Chunk = 20;

    // server|Type/id: the JSON, or null when the server says it does not exist
    private readonly ConcurrentDictionary<string, string?> _known = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async Task<FetchedReferences> FetchAsync(IViewerServer server, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        var found = new List<RelatedResource>();
        var missing = new List<string>();
        var asked = keys.Where(key => s_types.Contains(TypeOf(key))).Distinct(StringComparer.Ordinal).ToList();
        foreach (var key in asked.Where(key => _known.ContainsKey(Known(server, key))).ToList())
        {
            Add(key, _known[Known(server, key)], found, missing);
            asked.Remove(key);
        }

        foreach (var group in asked.GroupBy(TypeOf))
        {
            foreach (var chunk in group.Select(IdOf).Chunk(Chunk))
            {
                var returned = await SearchAsync(server, group.Key, chunk, cancellationToken);
                foreach (var id in chunk)
                {
                    var key = $"{group.Key}/{id}";
                    var json = returned is not null && returned.TryGetValue(id, out var item) ? item : await ReadAsync(server, group.Key, id, cancellationToken);
                    if (json is { Length: 0 })
                    {
                        continue;
                    }

                    _known[Known(server, key)] = json;
                    Add(key, json, found, missing);
                }
            }
        }

        return new FetchedReferences(found, missing);
    }

    private static void Add(string key, string? json, List<RelatedResource> found, List<string> missing)
    {
        if (json is null)
        {
            missing.Add(key);
        }
        else
        {
            found.Add(new RelatedResource(json, null));
        }
    }

    // By id; null when the search failed
    private async Task<Dictionary<string, string>?> SearchAsync(IViewerServer server, string type, string[] ids, CancellationToken cancellationToken)
    {
        try
        {
            var page = await reader.SearchAsync(server, type, FhirSearchState.Parse($"_id={string.Join(',', ids)}&_count={ids.Length}"), null, cancellationToken);
            return page.Items.Where(item => item.Json is not null).GroupBy(item => item.Id, StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.First().Json!, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogInformation(ex, "The {Type} the records refer to cannot be searched by id", type);
            return null;
        }
    }

    // The JSON; null when the server says there is no such resource; empty when nothing is known (an error, a refusal)
    private async Task<string?> ReadAsync(IViewerServer server, string type, string id, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadAsync(server, type, id, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogInformation(ex, "{Type}/{Id} the records refer to cannot be read", type, id);
            return string.Empty;
        }
    }

    private static string Known(IViewerServer server, string key)
    {
        return $"{server.Key}|{key}";
    }

    private static string TypeOf(string key)
    {
        return key[..key.IndexOf('/', StringComparison.Ordinal)];
    }

    private static string IdOf(string key)
    {
        return key[(key.IndexOf('/', StringComparison.Ordinal) + 1)..];
    }
}
