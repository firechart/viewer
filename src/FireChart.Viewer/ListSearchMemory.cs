using System.Text.Json;
using FireChart.Viewer.Core.Fhir;
using Flox.Core.Data;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FireChart.Viewer;

/// <summary>
/// The last search of the list of every resource type of a server: the list that opens without a search in its address takes it again.
/// </summary>
/// <remarks>
/// A search is kept as the query of the address (<c>kind=resource&amp;name:contains=Pat</c>), which is all a search form is. An empty query is a
/// list the user cleared and differs from none (the list was never opened, and the defaults of its form apply).
/// </remarks>
/// <example>
/// <code>
/// var last = await memory.GetAsync(server, "StructureDefinition");
/// await memory.SetAsync(server, "StructureDefinition", "kind=resource");
/// </code>
/// </example>
public interface IListSearchMemory
{
    /// <summary>
    /// The last search of a list.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="list">The list: the resource type, or another name of a list of its own.</param>
    /// <returns>The query; empty when the user cleared the search; <see langword="null"/> when there is none.</returns>
    ValueTask<string?> GetAsync(IViewerServer server, string list);

    /// <summary>
    /// Keeps the search of a list.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="list">The list: the resource type, or another name of a list of its own.</param>
    /// <param name="query">The query of the address without the question mark; empty for a cleared search.</param>
    /// <returns>A task that completes when the search is kept.</returns>
    ValueTask SetAsync(IViewerServer server, string list, string query);
}

/// <summary>
/// The <see cref="IListSearchMemory"/> in the <see cref="ILocalDocumentStore"/> of the host (IndexedDB of the browser, the store of the bookmarks and
/// the recent pages too), so a search outlives a reload and the end of the session. Scoped: the searches are read once per scope and kept in memory.
/// </summary>
/// <param name="store">The documents of the device.</param>
/// <param name="legacy">The <c>localStorage</c> of the host, where the searches were kept before; a search found there is moved to <paramref name="store"/>
/// the first time its list asks for it. None: nothing is moved.</param>
/// <remarks>
/// The search of a list of patients may name one (<c>family=Smith</c>): it stays on the device until the user changes the search or clears the data of
/// the site; it never reaches a server. Where the store is not there (prerendering, a closed circuit) nothing is kept and nothing is found.
/// </remarks>
/// <example>
/// <code>
/// var memory = new LocalListSearchMemory(new InMemoryDocumentStore());
/// await memory.SetAsync(server, "Patient", "family=Smith");
/// var last = await memory.GetAsync(server, "Patient"); // family=Smith
/// </code>
/// </example>
[FloxService(typeof(IListSearchMemory), ServiceLifetime.Scoped)]
public sealed class LocalListSearchMemory(ILocalDocumentStore store, ILocalStorage? legacy = null) : IListSearchMemory
{
    /// <summary>The collection of the searches in the <see cref="ILocalDocumentStore"/>.</summary>
    public const string Collection = "ViewerListSearch";

    private const string LegacyPrefix = "fx-viewer.search|";

    private Dictionary<string, string>? _searches;

    /// <inheritdoc/>
    public async ValueTask<string?> GetAsync(IViewerServer server, string list)
    {
        var key = KeyOf(server, list);
        try
        {
            var searches = await SearchesAsync();
            if (searches.TryGetValue(key, out var query))
            {
                return query;
            }

            return await MoveLegacyAsync(key);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync(IViewerServer server, string list, string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var key = KeyOf(server, list);
        try
        {
            var searches = await SearchesAsync();
            if (searches.TryGetValue(key, out var kept) && kept == query)
            {
                return;
            }

            await store.PutAsync(Collection, [new LocalDocument(key, JsonSerializer.Serialize(query))]);
            searches[key] = query;
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            // the search is not kept: the list works without it
        }
    }

    private async ValueTask<Dictionary<string, string>> SearchesAsync()
    {
        if (_searches is not null)
        {
            return _searches;
        }

        var searches = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var document in await store.LoadAsync(Collection))
        {
            try
            {
                if (JsonSerializer.Deserialize<string>(document.Json) is { } query)
                {
                    searches[document.Key] = query;
                }
            }
            catch (JsonException)
            {
                // a broken record is a search not kept
            }
        }

        _searches = searches;
        return searches;
    }

    // A search kept in localStorage by an older version: moved here, and removed there
    private async ValueTask<string?> MoveLegacyAsync(string key)
    {
        if (legacy is null || await legacy.GetAsync(LegacyPrefix + key) is not { } query)
        {
            return null;
        }

        await store.PutAsync(Collection, [new LocalDocument(key, JsonSerializer.Serialize(query))]);
        _searches![key] = query;
        await legacy.RemoveAsync(LegacyPrefix + key);
        return query;
    }

    private static bool IsUnavailable(Exception ex)
    {
        return ex is JSException or InvalidOperationException or JSDisconnectedException;
    }

    private static string KeyOf(IViewerServer server, string list)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(list);
        return $"{server.Key}|{server.BaseUrl.TrimEnd('/')}|{list}";
    }
}
