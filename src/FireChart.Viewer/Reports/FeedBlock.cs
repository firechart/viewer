using System.Diagnostics;
using System.Net;
using FireChart.Viewer.Components;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Core.Reports;
using FireChart.Viewer.Core.Search;
using FireChart.Viewer.Pages;
using FireChart.Viewer.Routing;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Reports;

// A section of a summary: the records its Queries find about the record of the page (%context), newest first, a page at a time (More), in the
// shell of the sections of the summary (the header with the count and the toggle, the failure with Retry). Each record is drawn by the tile of its type
// (Show); a record opens in a sheet over the page or on its own page, or as its summary when its type has one. A record that does not refer to the
// record of the page is left out (a server that ignores a parameter answers with everything)
internal sealed class FeedBlock : ReportBlock, IReportFeed, IDisposable
{
    // How many more pages one load asks for when a server that ignored the parameter of the record gave a page of other records only
    private const int MaxSkippedPages = 4;

    private static readonly ReportNode s_tile = ReportXml.Parse("""<Component name="Row"><Show value="{{%row}}" mode="tile"/></Component>""").Body[0];

    private readonly Dictionary<string, bool> _summaries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FhirNeeds?> _needs = new(StringComparer.Ordinal);

    // The scope of the tile of a record, made once: a row drawn again with the same scope reads nothing again
    private readonly Dictionary<ResourceSummary, ReportScope> _rows = new(ReferenceEqualityComparer.Instance);

    // The cells of a result of the laboratory, written once per record (again when the words of the codes come): the table is drawn at every page
    private readonly Dictionary<ResourceSummary, LabCells> _cells = new(ReferenceEqualityComparer.Instance);

    // What the searches brought with their records (_include: the results of the reports, the encounters)
    private readonly List<RelatedResource> _related = [];
    private readonly Dictionary<string, IReadOnlyList<ResourceSummary>> _history = new(StringComparer.Ordinal);
    private string _display = SummaryComponents.Tiles;
    private TileOptions _options = TileOptions.Default;
    private int? _decimals;
    private IReadOnlyList<LabGroup> _groups = [];
    private IReadOnlyList<TrendGroup> _trends = [];
    private string? _selected;
    private bool _trendLoading;
    private SummarySection? _section;
    private string _contextType = string.Empty;
    private string _contextId = string.Empty;
    private string? _patientId;

    [CascadingParameter]
    public ReportPage? Page { get; set; }

    // How long a section takes to load, to sort, to group and to draw, by its code and its counts only (no PHI)
    [Inject]
    private ILogger<FeedBlock> Logger { get; set; } = default!;

    private int _round;
    private Task? _filling;
    private Stopwatch? _drawing;

    public SummarySection? Section => _section;

    protected override IEnumerable<ReportValue?> Reads => DeepReads;

    protected override async Task PrepareAsync()
    {
        // Built once: a later render of the page changes nothing of what it has loaded
        if (_section is not null)
        {
            return;
        }

        if (Values.Context is not { Poco: Resource { Id: { } id } record } context)
        {
            throw new InvalidOperationException("a Feed needs the record of the page (%context)");
        }

        _contextType = record.TypeName;
        _contextId = id;
        _patientId = _contextType == SummaryContexts.Patient
            ? id
            : ReportText.Children(context, "subject").FirstOrDefault() is { } subject && ReportText.Children(subject, "reference").FirstOrDefault()?.Poco is PrimitiveType reference
              && reference.JsonValue?.ToString()?.Split("/_history/", 2)[0].Split('/') is [.., "Patient", var patient]
                ? patient
                : null;

        var title = Text("title");
        _display = Text("display") is { Length: > 0 } display ? display : SummaryComponents.Tiles;
        _decimals = int.TryParse(Text("decimals"), out var decimals) ? decimals : null;
        var definition = new SummaryFormSection
        {
            Code = Text("code") is { Length: > 0 } code ? code : title,
            CollapsedByDefault = Text("collapsed") == "true",
            HideIfEmpty = Text("hideIfEmpty") == "true",
            HideIfUnsupported = Text("hideIfUnsupported") != "false",
            OpenIn = Text("open") is { Length: > 0 } open ? open : null,
        };
        definition.Title.SetDefault(title);
        var searches = Node.Children.Select(query => new SummaryFormSearch
        {
            ResourceType = Values.Text(query.Attribute("type")),
            Query = Values.Text(query.Attribute("params")) is { Length: > 0 } parameters ? parameters : null,
            ContextParam = Values.Text(query.Attribute("param")) is { Length: > 0 } param ? param : null,
            Sort = Values.Text(query.Attribute("sort")) is { Length: > 0 } sort ? sort : null,
            PageSize = int.TryParse(Values.Text(query.Attribute("pageSize")), out var size) ? size : null,
        }).ToList();
        // What the tiles show besides their components: the age of the patient at the record, the status, the code of one of the systems before the
        // title, the decimals of the section
        _options = new TileOptions(Text("age") == "true", Text("status") != "false",
                                   Text("codes").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), _decimals);
        _section = new SummarySection(definition, searches, Scope.Runtime.Culture.Name, _options, _contextType, Session?.Fhir.EffectivePageSize());

        // What the tiles of the records show of others comes with the records (_include): one request instead of one per record. A tile that shows
        // the encounter of its record asks for {Type}:encounter; a server that refuses it is asked again without it (the reader remembers)
        foreach (var type in _section.Searches.Select(search => search.ResourceType).Distinct())
        {
            // The results of a report come with it: the table groups them under their report
            var tile = await Scope.Runtime.ModeAsync(type, "tile");
            _needs[type] = ReportNeeds.OfTile(tile, type, _display == SummaryComponents.LabResults && type == "DiagnosticReport" ? [LabResults.ReportResults] : []);
        }

        Page?.Add(this);
        await FillAsync();
    }

    public void SetCollapsed(bool collapsed)
    {
        if (_section is not null)
        {
            _section.Collapsed = collapsed;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    public Task MoreAsync()
    {
        // One load at a time: a second ask while the pages come (Load all, Print) waits for them, else both would add the same records
        return _section is null ? Task.CompletedTask : _filling is { IsCompleted: false } running ? running : _filling = FillAsync();
    }

    // The pages a load asks for: the first ones, the next ones, or after a failure the ones that failed
    private async Task FillAsync()
    {
        var section = _section!;
        section.IsLoading = true;
        Changed();
        var round = ++_round;
        var watch = Stopwatch.StartNew();
        try
        {
            var retry = section.Failed;
            var todo = section.Searches.Where(search => !search.Unsupported && (!search.IsLoaded || search.Failed || (!retry && search.Next is not null))).ToList();
            var pages = await Task.WhenAll(todo.Select(search => PageOfAsync(section, search)));
            var pagesMs = watch.ElapsedMilliseconds;

            // Whether the records open as the summaries of their types: known before they are drawn (a row drawn once is not drawn again)
            foreach (var type in pages.SelectMany(items => items).Select(item => item.ResourceType).Distinct().Where(type => !_summaries.ContainsKey(type)))
            {
                _summaries[type] = await Scope.Runtime.ModeAsync(type, "summary") is not null;
            }

            var modesMs = watch.ElapsedMilliseconds - pagesMs;
            section.Add(pages.SelectMany(items => items));
            section.Failed = section.Searches.Any(search => search.Failed);
            var sortMs = watch.ElapsedMilliseconds - pagesMs - modesMs;
            Logger.LogInformation("Summary section {Section}, round {Round}: {Searches} pages read in {PagesMs} ms, modes in {ModesMs} ms, {Records} records sorted in {SortMs} ms",
                                  section.Code, round, todo.Count, pagesMs, modesMs, section.Items.Count, sortMs);

            if (_display == SummaryComponents.LabResults)
            {
                List<RelatedResource> included;
                lock (_related)
                {
                    included = [.. _related];
                }

                var grouping = Stopwatch.StartNew();
                _groups = LabResults.Group(section.Items, LabResults.ObservationsOf(included), TimeZoneInfo.Local);
                Logger.LogInformation("Summary section {Section}, round {Round}: {Groups} groups of {Records} records and {Related} related in {GroupMs} ms",
                                      section.Code, round, _groups.Count, section.Items.Count, included.Count, grouping.ElapsedMilliseconds);
            }
            else if (_display == SummaryComponents.ObservationTrends)
            {
                var grouping = Stopwatch.StartNew();
                _trends = ObservationTrends.Group(section.Items);
                Logger.LogInformation("Summary section {Section}, round {Round}: {Trends} measures of {Records} records in {GroupMs} ms",
                                      section.Code, round, _trends.Count, section.Items.Count, grouping.ElapsedMilliseconds);
                if (_selected is null || _trends.All(trend => trend.Key != _selected))
                {
                    _selected = _trends.Count > 0 ? _trends[0].Key : null;
                }

                await LoadTrendAsync();
            }
        }
        finally
        {
            section.IsLoading = false;
            Logger.LogInformation("Summary section {Section}, round {Round}: done in {RoundMs} ms", section.Code, round, watch.ElapsedMilliseconds);
            Changed();
        }
    }

    private void Changed()
    {
        Page?.NotifyChanged();
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task<IReadOnlyList<ResourceSummary>> PageOfAsync(SummarySection section, SectionSearch search)
    {
        var data = Scope.Runtime.Data;
        try
        {
            var page = search.IsLoaded ? await data.NextPageAsync(search.Next!, search.ResourceType) : await FirstPageAsync(search);
            lock (_related)
            {
                _related.AddRange(page.Related ?? []);
            }

            var kept = Accept(search, page);
            for (var skipped = 0; kept.Count == 0 && page.Items.Count > 0 && page.Next is not null && skipped < MaxSkippedPages; skipped++)
            {
                page = await data.NextPageAsync(page.Next, search.ResourceType);
                kept = Accept(search, page);
            }

            search.IsLoaded = true;
            search.Failure = null;
            return kept;
        }
        catch (HttpRequestException ex) when (section.HideIfUnsupported && ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
        {
            search.Unsupported = true;
            return [];
        }
        catch (OperationCanceledException)
        {
            search.Failure = new SearchFailure(SearchFailureKind.Cancelled, null, null);
            return [];
        }
        catch (Exception ex)
        {
            search.Failure = SearchFailure.Of(ex, data.Server);
            return [];
        }
    }

    // The summary of an encounter asks for its patient too; a server that refuses the parameter patient for the type is asked again without it
    private async Task<ResourcePage> FirstPageAsync(SectionSearch search)
    {
        var patient = _contextType == SummaryContexts.Patient ? null : _patientId;
        try
        {
            var result = await Scope.Runtime.Data.FirstPageAsync(search.ResourceType, FhirSearchState.Parse(search.FirstQuery(_contextId, sorted: false, patient)), search.Sort,
                                                                 _needs.GetValueOrDefault(search.ResourceType));
            search.ServerSorted = result.Sorted || search.Sort.Length == 0;
            return result.Page;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && patient is not null && !search.WithoutPatient)
        {
            search.WithoutPatient = true;
            return await FirstPageAsync(search);
        }
    }

    // The history of the chosen measure: a search by its code, newest first; a server that refuses it shows the records of the section
    private async Task LoadTrendAsync()
    {
        if (_trends.FirstOrDefault(trend => trend.Key == _selected) is not { } trend || _history.ContainsKey(trend.Key))
        {
            return;
        }

        IReadOnlyList<ResourceSummary> history = trend.Records;
        if (trend.Search is { } code && _section!.Searches.Count > 0)
        {
            _trendLoading = true;
            Changed();
            try
            {
                var patient = _contextType == SummaryContexts.Patient ? null : _patientId;
                var query = _section.Searches[0].HistoryQuery(_contextId, code, ObservationTrends.HistorySize, patient);
                var result = await Scope.Runtime.Data.FirstPageAsync("Observation", FhirSearchState.Parse(query), ObservationTrends.HistorySort);
                var key = $"{_contextType}/{_contextId}";
                history = [.. result.Page.Items.Where(item => SummarySections.RefersTo(item.Json, key)).Concat(trend.Records).DistinctBy(item => item.Id)];
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // the chart shows the records of the section
            }
            finally
            {
                _trendLoading = false;
            }
        }

        _history[trend.Key] = history;
        Changed();
    }

    private async Task SelectTrendAsync(string key)
    {
        _selected = key;
        Changed();
        await LoadTrendAsync();
    }

    private List<ResourceSummary> Accept(SectionSearch search, ResourcePage page)
    {
        search.Next = page.Next;
        var key = $"{_contextType}/{_contextId}";
        return [.. page.Items.Where(item => item.Json is not null && SummarySections.RefersTo(item.Json, key))];
    }

    // A sheet over the summary, the summary of the record when its type has one, else its page
    private string? HrefOf(PocoNode node)
    {
        if (Session is null || node.Poco is not Resource { Id: { } id } resource || _section is null)
        {
            return null;
        }

        var record = new ViewerRoute(ViewerPage.Resource, resource.TypeName, id);
        var route = _section.OpensInSheet(resource.TypeName)
            ? new ViewerRoute(ViewerPage.Summary, _contextType, _contextId).WithPeek(record)
            : _summaries.GetValueOrDefault(resource.TypeName) ? new ViewerRoute(ViewerPage.Summary, resource.TypeName, id) : record;
        return ViewerRoutes.IsSafe(route.Location) ? Session.HrefOf(route) : null;
    }

    // From the start of the build of the section to the end of its render (its rows and their tiles drawn and applied)
    protected override void OnAfterRender(bool firstRender)
    {
        if (_drawing is { } drawing && _section is { } section)
        {
            Logger.LogInformation("Summary section {Section}: {Records} records drawn in {DrawMs} ms", section.Code, section.Items.Count, drawing.ElapsedMilliseconds);
        }

        _drawing = null;
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        _drawing = Stopwatch.StartNew();
        if (_section is null || _section.Hidden)
        {
            return;
        }

        var section = _section;
        builder.OpenComponent<SectionShell>(0);
        builder.AddComponentParameter(1, nameof(SectionShell.Section), section);
        builder.AddComponentParameter(2, nameof(SectionShell.OnToggle), EventCallback.Factory.Create(this, () => SetCollapsed(!section.Collapsed)));
        builder.AddComponentParameter(3, nameof(SectionShell.OnMore), EventCallback.Factory.Create(this, MoreAsync));
        builder.AddComponentParameter(4, nameof(SectionShell.ChildContent), (RenderFragment)Rows);
        builder.CloseComponent();
    }

    // The tile of a record by the component of its type; a row of a list leads where HrefOf says, a choice of a measure leads nowhere (its button chooses)
    private RenderFragment TileOf(ResourceSummary item, Func<PocoNode, string?> hrefOf)
    {
        return builder =>
        {
            builder.OpenComponent<ShowBlock>(0);
            builder.AddComponentParameter(1, nameof(ReportBlock.Node), s_tile);
            builder.AddComponentParameter(2, nameof(ReportBlock.Scope), RowScopeOf(item));
            builder.AddComponentParameter(3, nameof(ShowBlock.HrefOf), hrefOf);
            builder.CloseComponent();
        };
    }

    private ReportScope RowScopeOf(ResourceSummary item)
    {
        if (!_rows.TryGetValue(item, out var scope))
        {
            scope = Scope.With("row", [Scope.Runtime.Data.NodeOf(item)]).WithOptions(_options);
            _rows[item] = scope;
        }

        return scope;
    }

    private string? HrefOf(ResourceSummary item)
    {
        return item.Json is null ? null : HrefOf(Scope.Runtime.Data.NodeOf(item));
    }

    // A cell of a result of the laboratory: the test, the value (and the values of its components) with the decimals of the section, the first
    // reference range (its text, else low - high with the unit once, else > low or < high)
    private string LabText(ResourceSummary item, string cell)
    {
        return CellsOf(item) is not { } cells
            ? string.Empty
            : cell switch
            {
                "test" => cells.Test,
                "value" => cells.Value,
                "range" => cells.Range,
                _ => string.Empty
            };
    }

    private LabCells? CellsOf(ResourceSummary item)
    {
        if (item.Json is null)
        {
            return null;
        }

        var generation = Scope.Runtime.Generation;
        if (!_cells.TryGetValue(item, out var cells) || cells.Generation != generation)
        {
            cells = new LabCells(generation, WriteCell(item, "test"), WriteCell(item, "value"), WriteCell(item, "range"),
                                 ReportText.Children(Scope.Runtime.Data.NodeOf(item), "interpretation").Any());
            _cells[item] = cells;
        }

        return cells;
    }

    private sealed record LabCells(int Generation, string Test, string Value, string Range, bool Flagged);

    private string WriteCell(ResourceSummary item, string cell)
    {
        var node = Scope.Runtime.Data.NodeOf(item);
        var culture = Scope.Runtime.Format;
        switch (cell)
        {
            case "test":
                return ReportText.Text(node, "code", culture);
            case "value":
                var values = ReportText.Children(node, "value").Concat(ReportText.Children(node, "component").SelectMany(part => ReportText.Children(part, "value")))
                                       .Select(value => Rounded(value, culture)).Where(text => text.Length > 0).ToList();
                return values.Count > 0 ? string.Join(" / ", values) : ReportText.Text(node, "dataAbsentReason", culture);
            case "range":
                if (ReportText.Children(node, "referenceRange").FirstOrDefault() is not { } range)
                {
                    return string.Empty;
                }

                var low = ReportText.Children(range, "low").FirstOrDefault();
                var high = ReportText.Children(range, "high").FirstOrDefault();
                return ReportText.Text(range, "text", culture) is { Length: > 0 } text ? text
                    : low is not null && high is not null ? $"{Number(low, culture)} - {Number(high, culture)} {ReportText.Text(high, "unit", culture)}".TrimEnd()
                    : low is not null ? "> " + Rounded(low, culture)
                    : high is not null ? "< " + Rounded(high, culture)
                    : string.Empty;
            default:
                return string.Empty;
        }
    }

    // A quantity with its value at most the decimals of the section, and its unit
    private string Rounded(PocoNode value, ReportFormat culture)
    {
        return value.Poco.TypeName is "Quantity" or "SimpleQuantity"
            ? $"{Number(value, culture)} {ReportText.Text(value, "unit", culture)}".TrimEnd()
            : ReportText.Of(value, culture);
    }

    private string Number(PocoNode quantity, ReportFormat format)
    {
        var value = ReportText.Children(quantity, "value").FirstOrDefault()?.Poco as FhirDecimal;
        return value?.Value is { } number
            ? DecimalText.Format(number, _decimals ?? format.Decimals, format.Culture)
            : string.Empty;
    }

    private bool HasFlag(ResourceSummary item)
    {
        return CellsOf(item)?.Flagged == true;
    }

    private RenderFragment Flag(ResourceSummary item)
    {
        return builder =>
        {
            if (item.Json is null)
            {
                return;
            }

            foreach (var interpretation in ReportText.Children(Scope.Runtime.Data.NodeOf(item), "interpretation"))
            {
                ReportBlocks.Badge(builder, ReportText.Of(interpretation, Scope.Runtime.Format), ReportTones.Interpretation(ReportTones.CodeOf(interpretation)));
            }
        };
    }

    private void Rows(RenderTreeBuilder builder)
    {
        if (_display == SummaryComponents.LabResults)
        {
            builder.OpenComponent<ReportLab>(20);
            builder.AddComponentParameter(21, nameof(ReportLab.Section), _section!);
            builder.AddComponentParameter(22, nameof(ReportLab.Groups), _groups);
            builder.AddComponentParameter(23, nameof(ReportLab.Tile), (RenderFragment<ResourceSummary>)(item => TileOf(item, HrefOf)));
            builder.AddComponentParameter(24, nameof(ReportLab.Text), (Func<ResourceSummary, string, string>)LabText);
            builder.AddComponentParameter(25, nameof(ReportLab.Flag), (RenderFragment<ResourceSummary>)Flag);
            builder.AddComponentParameter(26, nameof(ReportLab.HasFlag), (Func<ResourceSummary, bool>)HasFlag);
            builder.AddComponentParameter(27, nameof(ReportLab.HrefOf), (Func<ResourceSummary, string?>)HrefOf);
            builder.CloseComponent();
            return;
        }

        if (_display == SummaryComponents.ObservationTrends)
        {
            builder.OpenComponent<ReportTrends>(30);
            builder.AddComponentParameter(31, nameof(ReportTrends.Section), _section!);
            builder.AddComponentParameter(32, nameof(ReportTrends.Trends), _trends);
            builder.AddComponentParameter(33, nameof(ReportTrends.Selected), _selected);
            builder.AddComponentParameter(34, nameof(ReportTrends.History), _history);
            builder.AddComponentParameter(35, nameof(ReportTrends.Loading), _trendLoading);
            builder.AddComponentParameter(36, nameof(ReportTrends.Decimals), _decimals ?? Session?.Fhir.Decimals);
            builder.AddComponentParameter(37, nameof(ReportTrends.Tile), (RenderFragment<ResourceSummary>)(item => TileOf(item, _ => null)));
            builder.AddComponentParameter(38, nameof(ReportTrends.TitleOf),
                                          (Func<ResourceSummary, string>)(item => item.Json is null ? item.Id : ReportText.Text(Scope.Runtime.Data.NodeOf(item), "code", Scope.Runtime.Format)));
            builder.AddComponentParameter(39, nameof(ReportTrends.HrefOf), (Func<ResourceSummary, string?>)HrefOf);
            builder.AddComponentParameter(40, nameof(ReportTrends.OnSelect), EventCallback.Factory.Create<string>(this, SelectTrendAsync));
            builder.CloseComponent();
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "role", "list");
        foreach (var item in _section!.Items)
        {
            builder.OpenElement(2, "div");
            builder.SetKey(item);
            builder.AddAttribute(3, "class", "fx-summary-section-row");
            builder.AddAttribute(4, "role", "listitem");
            builder.OpenComponent<ShowBlock>(5);
            builder.AddComponentParameter(6, nameof(ReportBlock.Node), s_tile);
            builder.AddComponentParameter(7, nameof(ReportBlock.Scope), RowScopeOf(item));
            builder.AddComponentParameter(8, nameof(ShowBlock.HrefOf), (Func<PocoNode, string?>)HrefOf);
            builder.CloseComponent();
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    public void Dispose()
    {
        Page?.Remove(this);
    }
}
