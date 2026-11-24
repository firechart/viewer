using System.Collections.Concurrent;
using System.Diagnostics;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Search;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Core.Reports;

// The requests of one report: a search is asked once per distinct query, however many blocks wait for it, and pages are followed up to its limit.
// Cancelled with the report (the page left). The log tells how long a page took to read and to parse, by its type and its counts only: never an id, a
// reference, a value or an address (PHI)
internal sealed partial class ReportData(IFhirServerReader reader, IViewerServer server, ModelInspector model, CancellationToken cancellationToken,
                                         ILogger? logger = null)
{
    // The records already known by Type/id: read, resolved, or brought by a search; a reference to one of them asks nothing
    private readonly ConcurrentDictionary<string, Lazy<Task<PocoNode?>>> _records = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _missing = new(StringComparer.Ordinal);

    public ReportProgress Progress { get; } = new();

    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<PocoNode>>>> _searches = new(StringComparer.Ordinal);

    public ModelInspector Model => model;

    public IViewerServer Server => server;

    public Task<IReadOnlyList<PocoNode>> SearchAsync(string query, int max)
    {
        return _searches.GetOrAdd($"{max}|{query}", _ => new Lazy<Task<IReadOnlyList<PocoNode>>>(() => RunAsync(query, max))).Value;
    }

    public async Task<PocoNode> ReadAsync(string type, string id)
    {
        var record = ReportExpressions.Parse(await reader.ReadAsync(server, type, id, cancellationToken), model);
        Know(record);
        return record;
    }

    // A page of a feed (a section of a summary): the first one of a search, sorted when the server can, or the next one of a link. Its records and
    // what came with them become known, so a reference to one of them asks nothing
    public async Task<SortedResourcePage> FirstPageAsync(string type, FhirSearchState search, string sort, FhirNeeds? needs = null)
    {
        var watch = Stopwatch.StartNew();
        var page = await reader.SearchSortedAsync(server, type, search, sort, needs, cancellationToken);
        var read = watch.ElapsedMilliseconds;
        KnowAll(page.Page);
        Log("first", type, page.Page, read, watch.ElapsedMilliseconds - read);
        return page;
    }

    public async Task<ResourcePage> NextPageAsync(string link, string type)
    {
        var watch = Stopwatch.StartNew();
        var page = await reader.GetPageAsync(server, link, type, cancellationToken);
        var read = watch.ElapsedMilliseconds;
        KnowAll(page);
        Log("next", type, page, read, watch.ElapsedMilliseconds - read);
        return page;
    }

    private void Log(string kind, string type, ResourcePage page, long read, long parsed)
    {
        logger?.LogInformation("Summary data: the {Kind} page of {Type}, {Records} records and {Related} related, read in {ReadMs} ms, parsed in {ParseMs} ms",
                               kind, type, page.Items.Count, page.Related?.Count ?? 0, read, parsed);
    }

    // A record of a page as a node of the release, parsed once, when it is first needed (a record that came with a page and its tile share it)
    public PocoNode NodeOf(ResourceSummary item)
    {
        return Lazily($"{item.ResourceType}/{item.Id}", item.Json!).Value;
    }

    private readonly ConcurrentDictionary<string, Lazy<PocoNode>> _nodes = new(StringComparer.Ordinal);

    private Lazy<PocoNode> Lazily(string key, string json)
    {
        return _nodes.GetOrAdd(key, _ => new Lazy<PocoNode>(() => ReportExpressions.Parse(json, model)));
    }

    // The records of a page and what came with it are known, parsed only when a block needs one (most of the results of the reports of a summary are
    // never drawn as a tile); a record whose type and id cannot be told without parsing it is parsed at once
    private void KnowAll(ResourcePage page)
    {
        foreach (var item in page.Items.Where(item => item.Json is not null))
        {
            KnowLazily($"{item.ResourceType}/{item.Id}", item.Json!);
        }

        KnowRelated(page.Related ?? []);
    }

    // What a search of the page brought with its records (_include): known, so a tile that refers to one reads nothing
    public void KnowRelated(IEnumerable<RelatedResource> related)
    {
        foreach (var resource in related)
        {
            if (KeyOfJson(resource.Json) is { } key)
            {
                KnowLazily(key, resource.Json);
            }
            else
            {
                Know(ReportExpressions.Parse(resource.Json, model));
            }
        }
    }

    private void KnowLazily(string key, string json)
    {
        var node = Lazily(key, json);
        _records.TryAdd(key, new Lazy<Task<PocoNode?>>(() => ParsedAsync(node)));
    }

    // A record that cannot be parsed cannot be read: its reference is shown as it is
    private static Task<PocoNode?> ParsedAsync(Lazy<PocoNode> node)
    {
        try
        {
            return Task.FromResult<PocoNode?>(node.Value);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or System.Text.Json.JsonException)
        {
            return Task.FromResult<PocoNode?>(null);
        }
    }

    // Type/id of a resource from the members resourceType and id of its root, without parsing it into the model
    private static string? KeyOfJson(string json)
    {
        try
        {
            var reader = new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
            string? type = null, id = null;
            if (!reader.Read() || reader.TokenType != System.Text.Json.JsonTokenType.StartObject)
            {
                return null;
            }

            while (reader.Read() && reader.TokenType == System.Text.Json.JsonTokenType.PropertyName && (type is null || id is null))
            {
                var name = reader.GetString();
                reader.Read();
                if (name == "resourceType" && reader.TokenType == System.Text.Json.JsonTokenType.String)
                {
                    type = reader.GetString();
                }
                else if (name == "id" && reader.TokenType == System.Text.Json.JsonTokenType.String)
                {
                    id = reader.GetString();
                }
                else
                {
                    reader.Skip();
                }
            }

            return type is { Length: > 0 } && id is { Length: > 0 } ? $"{type}/{id}" : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    // A record already read (the page of a record has its JSON): parsed and known, not asked again
    public PocoNode Adopt(string json)
    {
        var record = ReportExpressions.Parse(json, model);
        Know(record);
        return record;
    }

    // The record a reference points to: one of this server (Type/id, or its own address), read once per report; null for a reference elsewhere, a
    // contained one, or a record that cannot be read (the reference is then shown as it is)
    public Task<PocoNode?> ResolveAsync(PocoNode reference)
    {
        var text = ReportText.Children(reference, "reference").FirstOrDefault()?.Poco is PrimitiveType value ? value.JsonValue?.ToString() : null;
        var key = text is null ? null : KeyOf(text);
        return key is null
            ? Task.FromResult<PocoNode?>(null)
            : _records.GetOrAdd(key, k => new Lazy<Task<PocoNode?>>(() => FetchAsync(k))).Value;
    }

    private string? KeyOf(string reference)
    {
        var local = reference.StartsWith(server.BaseUrl.TrimEnd('/') + "/", StringComparison.Ordinal) ? reference[(server.BaseUrl.TrimEnd('/').Length + 1)..] : reference;
        var match = Relative().Match(local.Split("/_history/", 2)[0]);
        return match.Success ? $"{match.Groups[1].Value}/{match.Groups[2].Value}" : null;
    }

    // Whether the server said the record a reference points to does not exist (404, 410); known once ResolveAsync has answered. An error of the
    // server or a reference that cannot be read here (another server, a contained resource) is not "missing"
    // The type of the missing record (Patient); null when it is not known to be missing
    public string? MissingTypeOf(PocoNode reference)
    {
        var text = ReportText.Children(reference, "reference").FirstOrDefault()?.Poco is PrimitiveType value ? value.JsonValue?.ToString() : null;
        return text is not null && KeyOf(text) is { } key && _missing.ContainsKey(key) ? key[..key.IndexOf('/', StringComparison.Ordinal)] : null;
    }

    private async Task<PocoNode?> FetchAsync(string key)
    {
        var parts = key.Split('/');
        try
        {
            return ReportExpressions.Parse(await reader.ReadAsync(server, parts[0], parts[1], cancellationToken), model);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
        {
            _missing.TryAdd(key, true);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or Flox.Core.FloxException or InvalidOperationException)
        {
            return null;
        }
    }

    private void Know(PocoNode record)
    {
        if (record.Poco is Resource { Id: { } id } resource)
        {
            _records.TryAdd($"{resource.TypeName}/{id}", new Lazy<Task<PocoNode?>>(Task.FromResult<PocoNode?>(record)));
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^([A-Z][A-Za-z]+)/([A-Za-z0-9\-.]{1,64})$")]
    private static partial System.Text.RegularExpressions.Regex Relative();

    private async Task<IReadOnlyList<PocoNode>> RunAsync(string query, int max)
    {
        var type = query.Split('?')[0];
        var link = $"{server.BaseUrl.TrimEnd('/')}/{query}";
        if (!link.Contains("_count=", StringComparison.Ordinal))
        {
            link += (link.Contains('?', StringComparison.Ordinal) ? "&" : "?") + $"_count={Math.Min(max, 100)}";
        }

        var found = new List<PocoNode>();
        string? next = link;
        while (next is not null && found.Count < max)
        {
            var page = await reader.GetPageAsync(server, next, type, cancellationToken);
            found.AddRange(page.Items.Where(item => item.Json is not null && item.ResourceType == type).Take(max - found.Count).Select(item => ReportExpressions.Parse(item.Json!, model)));
            next = page.Next;
        }

        foreach (var record in found)
        {
            Know(record);
        }

        Progress.Loaded(type, found.Count);
        return found;
    }
}
