using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FireChart.Viewer.Core.Search;
using Flox.Core;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// A resource of a search page.
/// </summary>
/// <param name="Id">The id of the resource.</param>
/// <param name="ResourceType">Its type.</param>
/// <param name="Json">The resource as the server sent it, what a list renders its row from; <see langword="null"/> when only the id is known.</param>
/// <example>
/// <code>
/// foreach (var item in page.Items)
/// {
///     Console.WriteLine($"{item.ResourceType}/{item.Id}");
/// }
/// </code>
/// </example>
public sealed record ResourceSummary(string Id, string ResourceType, string? Json = null);

/// <summary>
/// A resource that came in the <c>Bundle</c> with the ones that were searched for (<c>_include</c>, <c>_revinclude</c>): what the templates reach through a
/// <c>Reference</c>.
/// </summary>
/// <param name="Json">The resource as the server sent it.</param>
/// <param name="FullUrl">The <c>fullUrl</c> of its entry, or <see langword="null"/>.</param>
/// <example>
/// <code>
/// var pool = page.Related?.Select(related =&gt; related.Json).ToList() ?? [];
/// </code>
/// </example>
public sealed record RelatedResource(string Json, string? FullUrl);

/// <summary>
/// A page of a search. The links are those the server put in the <c>Bundle</c>; there is no page number in FHIR.
/// </summary>
/// <param name="Items">The resources of the type that was searched (not the included ones, not an <c>OperationOutcome</c>).</param>
/// <param name="Total">The <c>total</c> of the <c>Bundle</c>, when the server says it.</param>
/// <param name="First">The link <c>first</c>, or <see langword="null"/>.</param>
/// <param name="Previous">The link <c>previous</c> (or <c>prev</c>), or <see langword="null"/>.</param>
/// <param name="Next">The link <c>next</c>, or <see langword="null"/> on the last page.</param>
/// <param name="Last">The link <c>last</c>, or <see langword="null"/>.</param>
/// <param name="Related">The other resources of the <c>Bundle</c> (<see cref="RelatedResource"/>).</param>
/// <example>
/// <code>
/// var page = await reader.SearchAsync(server, "Observation");
/// while (page.Next is { } next)
/// {
///     page = await reader.GetPageAsync(server, next, "Observation");
/// }
/// </code>
/// </example>
public sealed record ResourcePage(IReadOnlyList<ResourceSummary> Items, long? Total, string? First, string? Previous, string? Next, string? Last, IReadOnlyList<RelatedResource>? Related = null);

/// <summary>
/// The first page of a search asked in an order (<see cref="IFhirServerReader.SearchSortedAsync"/>), and whether the server gave that order.
/// </summary>
/// <param name="Page">The page.</param>
/// <param name="Sorted"><see langword="true"/> when the page came in the order asked; <see langword="false"/> when the server refused it (or no order
/// was asked) and the records came in the order the server likes: the first page is then not the newest records, only some of them.</param>
/// <example>
/// <code>
/// var result = await reader.SearchSortedAsync(server, "Observation", FhirSearchState.Parse("patient=p1&amp;_count=20"), "-date");
/// var note = result.Sorted ? "newest first" : "in the order of the server";
/// </code>
/// </example>
public sealed record SortedResourcePage(ResourcePage Page, bool Sorted);

/// <summary>
/// Reads the resources of a FHIR server; read-only. The answers are release-agnostic JSON: a <c>Bundle</c> has the same shape in every release.
/// </summary>
/// <remarks>
/// The <c>Authorization</c> header comes from the <see cref="IServerCredentialStore"/> and goes only to the origin of the server. The errors of HTTP are
/// <see cref="HttpRequestException"/> with their status code; an answer of the wrong kind is a <see cref="FloxException"/>.
/// </remarks>
/// <example>
/// <code>
/// var page = await reader.SearchAsync(server, "Patient", FhirSearchState.Parse("family=Smith"));
/// var json = await reader.ReadAsync(server, "Patient", page.Items[0].Id);
/// </code>
/// </example>
public interface IFhirServerReader
{
    /// <summary>
    /// The first page of a search in a type. The server is not asked to count (it would go through every resource).
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="resourceType">The type (<c>Patient</c>): letters only.</param>
    /// <param name="search">The search; none is every resource of the type.</param>
    /// <param name="needs">What is asked to come with the resources (<c>_include</c>, <c>_revinclude</c>). A server that refuses them with a client error is
    /// asked again without them; when that answers, it is not asked with them again for that type (when it fails too, the includes were not the reason
    /// and the error of the second request is thrown).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentException"><paramref name="resourceType"/> is not a resource type.</exception>
    Task<ResourcePage> SearchAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, FhirNeeds? needs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The first page of a search in an order that the server may not know (a server may sort by a subset of the search parameters, and Blaze refuses
    /// the others with <c>400</c>). A refusal (a client error other than <c>401</c>, <c>403</c>, <c>404</c>) is answered by asking again, in this order:
    /// without the order and with the needs, with the order and without the needs, without both. What had to be left out is remembered for the
    /// server and the type (the order also by its value), so the next search asks for it no more: one request after the first.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="resourceType">The type (<c>Observation</c>): letters only.</param>
    /// <param name="search">The search without <c>_sort</c>.</param>
    /// <param name="sort">The order (<c>-date</c>), the value of <c>_sort</c>; empty asks for none.</param>
    /// <param name="needs">What is asked to come with the resources (<c>_include</c>, <c>_revinclude</c>).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page and whether it is in the order asked.</returns>
    /// <exception cref="ArgumentException"><paramref name="resourceType"/> is not a resource type.</exception>
    /// <exception cref="HttpRequestException">The last request failed: the error is not the order nor the needs.</exception>
    /// <example>
    /// <code>
    /// var result = await reader.SearchSortedAsync(server, "Condition", FhirSearchState.Parse("patient=p1&amp;_count=20"), "-recorded-date",
    ///     new FhirNeeds(["Condition:encounter"], []));
    /// </code>
    /// </example>
    Task<SortedResourcePage> SearchSortedAsync(IViewerServer server, string resourceType, FhirSearchState search, string sort, FhirNeeds? needs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one resource.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="resourceType">The type (<c>Patient</c>).</param>
    /// <param name="id">The id: letters, digits, <c>-</c> and <c>.</c>, up to 64 characters.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The resource as the server sent it (JSON text).</returns>
    /// <exception cref="ArgumentException">The type or the id is not valid.</exception>
    /// <exception cref="HttpRequestException">The resource does not exist (<c>404</c>) or the server refused (<c>401</c>, <c>403</c>).</exception>
    /// <exception cref="FloxException">The server answered with another resource type.</exception>
    Task<string> ReadAsync(IViewerServer server, string resourceType, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many resources a search finds, by a request of its own that returns no resources (<c>_summary=count</c>).
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="resourceType">The type.</param>
    /// <param name="search">The search; none counts every resource of the type.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The count, or <see langword="null"/> when the server does not say.</returns>
    Task<long?> CountAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page by a link of a <c>Bundle</c> (<see cref="ResourcePage.Next"/>).
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="link">The link; only a link to the origin of the server is followed, so the credentials go nowhere else.</param>
    /// <param name="resourceType">The type that was searched.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page.</returns>
    /// <exception cref="FloxException">The link points to another origin.</exception>
    Task<ResourcePage> GetPageAsync(IViewerServer server, string link, string resourceType, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IFhirServerReader"/> over HTTP; a singleton (it remembers the servers that refuse <c>_include</c>).
/// </summary>
/// <param name="httpClientFactory">Makes the client <see cref="FhirServerHttp.HttpClientName"/>.</param>
/// <param name="credentials">The <c>Authorization</c> of the servers.</param>
/// <example>
/// <code>
/// IFhirServerReader reader = new FhirServerReader(httpClientFactory, new ServerCredentialStore());
/// </code>
/// </example>
[FloxService(typeof(IFhirServerReader), ServiceLifetime.Singleton)]
public sealed partial class FhirServerReader(IHttpClientFactory httpClientFactory, IServerCredentialStore credentials) : IFhirServerReader
{
    [GeneratedRegex("^[A-Za-z]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ResourceTypeRegex();

    // The id of FHIR: letters, digits, - and ., up to 64 characters
    [GeneratedRegex("^[A-Za-z0-9.-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();

    // Servers (by address and type) that refused the includes, and (by address, type and order) that refused an order: not asked so again
    private readonly ConcurrentDictionary<string, byte> _withoutIncludes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _withoutSort = new(StringComparer.Ordinal);

    // One request of a sorted search: with the order or not, with the needs or not
    private sealed record SearchStep(bool Sorted, bool Included);

    /// <inheritdoc/>
    public async Task<ResourcePage> SearchAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, FhirNeeds? needs = null, CancellationToken cancellationToken = default)
    {
        var url = SearchUrl(server, resourceType, search);
        var key = $"{server.BaseUrl}|{resourceType}";
        if (needs is null || needs.IsEmpty || _withoutIncludes.ContainsKey(key))
        {
            return await ReadPageAsync(server, url, resourceType, cancellationToken);
        }

        try
        {
            return await ReadPageAsync(server, url + NeedsQuery(needs), resourceType, cancellationToken);
        }
        catch (HttpRequestException ex) when (IsRefusal(ex))
        {
            // Remembered only when it helps: a search refused for another reason (an order the server does not know) keeps its includes
            var page = await ReadPageAsync(server, url, resourceType, cancellationToken);
            _withoutIncludes[key] = 0;
            return page;
        }
    }

    /// <inheritdoc/>
    public async Task<SortedResourcePage> SearchSortedAsync(IViewerServer server, string resourceType, FhirSearchState search, string sort, FhirNeeds? needs = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(sort);
        var url = SearchUrl(server, resourceType, search);
        var key = $"{server.BaseUrl}|{resourceType}";
        var sortKey = $"{key}|{sort.Trim()}";
        var canSort = sort.Trim().Length > 0 && !_withoutSort.ContainsKey(sortKey);
        var canInclude = needs is { IsEmpty: false } && !_withoutIncludes.ContainsKey(key);
        List<SearchStep> ladder = [.. new SearchStep[] { new(canSort, canInclude), new(false, canInclude), new(canSort, false), new(false, false) }.Distinct()];

        for (var i = 0; ; i++)
        {
            var step = ladder[i];
            try
            {
                var page = await ReadPageAsync(server, url + (step.Sorted ? $"&_sort={Uri.EscapeDataString(sort.Trim())}" : string.Empty)
                                                          + (step.Included ? NeedsQuery(needs!) : string.Empty), resourceType, cancellationToken);
                if (canSort && !step.Sorted)
                {
                    _withoutSort[sortKey] = 0;
                }

                if (canInclude && !step.Included)
                {
                    _withoutIncludes[key] = 0;
                }

                return new SortedResourcePage(page, step.Sorted);
            }
            catch (HttpRequestException ex) when (i < ladder.Count - 1 && IsRefusal(ex))
            {
                // the next step leaves out more
            }
        }
    }

    // A client error that a search without the order or the needs may not get; not the credentials, not a type the server does not have and
    // not a server that is busy (what is left out is remembered for the session)
    private static bool IsRefusal(HttpRequestException ex)
    {
        return ex.StatusCode is { } status && (int)status is >= 400 and < 500
               && status is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.RequestTimeout
                   or HttpStatusCode.Conflict or HttpStatusCode.TooManyRequests);
    }

    private static string SearchUrl(IViewerServer server, string resourceType, FhirSearchState? search)
    {
        return $"{TypeUrl(server, resourceType)}?{(search ?? FhirSearchState.Empty).ToSearchQuery(server.PageSize)}{(server.SearchSummary ? "&_summary=true" : string.Empty)}";
    }

    private static string NeedsQuery(FhirNeeds needs)
    {
        return string.Concat(needs.Include.Select(value => $"&_include={Uri.EscapeDataString(value)}"))
               + string.Concat(needs.RevInclude.Select(value => $"&_revinclude={Uri.EscapeDataString(value)}"));
    }

    /// <inheritdoc/>
    public async Task<long?> CountAsync(IViewerServer server, string resourceType, FhirSearchState? search = null, CancellationToken cancellationToken = default)
    {
        return (await ReadPageAsync(server, $"{TypeUrl(server, resourceType)}?{(search ?? FhirSearchState.Empty).ToCountQuery()}", resourceType, cancellationToken)).Total;
    }

    /// <inheritdoc/>
    public async Task<string> ReadAsync(IViewerServer server, string resourceType, string id, CancellationToken cancellationToken = default)
    {
        if (!IdRegex().IsMatch(id))
        {
            throw new ArgumentException("Not a resource id.", nameof(id));
        }

        using var document = await FhirServerHttp.GetJsonAsync(httpClientFactory, $"{TypeUrl(server, resourceType)}/{id}", credentials.GetAuthorization(server), FhirServerHttp.TimeoutOf(server), cancellationToken);
        if (!document.RootElement.TryGetProperty("resourceType", out var type) || type.GetString() != resourceType)
        {
            throw new FloxException($"The server did not answer with a {resourceType}.");
        }

        return document.RootElement.GetRawText();
    }

    private static string TypeUrl(IViewerServer server, string resourceType)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (!ResourceTypeRegex().IsMatch(resourceType))
        {
            throw new ArgumentException("Not a resource type.", nameof(resourceType));
        }

        return $"{server.BaseUrl.TrimEnd('/')}/{resourceType}";
    }

    /// <inheritdoc/>
    public async Task<ResourcePage> GetPageAsync(IViewerServer server, string link, string resourceType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        EnsureSameOrigin(server.BaseUrl, link);
        return await ReadPageAsync(server, link, resourceType, cancellationToken);
    }

    internal static void EnsureSameOrigin(string baseUrl, string link)
    {
        var origin = new Uri(baseUrl);
        if (!Uri.TryCreate(link, UriKind.Absolute, out var target) ||
            target.Scheme != origin.Scheme ||
            !string.Equals(target.IdnHost, origin.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            target.Port != origin.Port)
        {
            throw new FloxException("The link points to another origin than the server.");
        }
    }

    private async Task<ResourcePage> ReadPageAsync(IViewerServer server, string url, string resourceType, CancellationToken cancellationToken)
    {
        using var document = await FhirServerHttp.GetJsonAsync(httpClientFactory, url, credentials.GetAuthorization(server), FhirServerHttp.TimeoutOf(server), cancellationToken);
        return Parse(document.RootElement, resourceType);
    }

    // Release-agnostic, like the types: a Bundle has the same shape in every release. Only the resources of the type that was asked for: the
    // Bundle may hold an OperationOutcome or included resources
    internal static ResourcePage Parse(JsonElement bundle, string resourceType)
    {
        if (bundle.ValueKind != JsonValueKind.Object || !bundle.TryGetProperty("resourceType", out var type) || type.GetString() != "Bundle")
        {
            throw new FloxException("The server did not answer with a Bundle.");
        }

        var items = new List<ResourceSummary>();
        var related = new List<RelatedResource>();
        if (bundle.TryGetProperty("entry", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("resource", out var resource) || !resource.TryGetProperty("resourceType", out var name) || name.GetString() is not { } kind)
                {
                    continue;
                }

                var included = entry.TryGetProperty("search", out var search) && search.TryGetProperty("mode", out var mode) && mode.GetString() == "include";
                if (kind == resourceType && !included && resource.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } value)
                {
                    items.Add(new ResourceSummary(value, resourceType, resource.GetRawText()));
                }
                else if (kind != "OperationOutcome")
                {
                    related.Add(new RelatedResource(resource.GetRawText(), entry.TryGetProperty("fullUrl", out var fullUrl) ? fullUrl.GetString() : null));
                }
            }
        }

        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        if (bundle.TryGetProperty("link", out var linkArray) && linkArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in linkArray.EnumerateArray())
            {
                if (link.TryGetProperty("relation", out var relation) && link.TryGetProperty("url", out var url) &&
                    relation.GetString() is { } key && url.GetString() is { Length: > 0 } target)
                {
                    links.TryAdd(key, target);
                }
            }
        }

        long? total = bundle.TryGetProperty("total", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt64(out var number) ? number : null;
        return new ResourcePage(items, total, links.GetValueOrDefault("first"), links.GetValueOrDefault("previous") ?? links.GetValueOrDefault("prev"), links.GetValueOrDefault("next"), links.GetValueOrDefault("last"), related);
    }
}
