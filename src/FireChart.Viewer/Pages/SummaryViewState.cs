using System.Net;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Routing;
using Flox.Shared.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Pages;

/// <summary>
/// What a summary shows: the record it grows from.
/// </summary>
/// <param name="Session">The instance of the Viewer.</param>
/// <param name="ContextType">The type of the record (<c>Patient</c>, <c>Encounter</c>): the name of its form, <c>summary-forms/{Type}.json</c>.</param>
/// <param name="ContextId">The id of the record.</param>
public sealed record SummaryKey(ViewerSession Session, string ContextType, string ContextId);

/// <summary>
/// The state of a summary: the banner of the patient first (and of the encounter, for the summary of an encounter), then the blocks of its form, each
/// from requests of its own, all at once. The form is the one of the type of the record (<see cref="ViewerSummaryForms"/>); a type without one has
/// no summary, and the page of the record shows instead. Activated with a <see cref="SummaryKey"/>.
/// </summary>
/// <example>
/// <code>
/// await state.ActivateAsync(new SummaryKey(session, "Encounter", "e1"));
/// var blocks = state.Sections;
/// </code>
/// </example>
public class SummaryViewState : State
{
    // Rounds of next pages of LoadAllAsync: a server whose next links never end stops there
    private const int MaxLoadAllRounds = 200;

    private readonly Dictionary<SummarySection, Task> _filling = [];
    private readonly IServerCredentialStore _credentials;
    private readonly IFhirServerReader _reader;
    private readonly IServerResourceTypeProvider _types;
    private readonly IResourceViews _views;
    private readonly IResourceTiles _tiles;
    private readonly ITerminologyResolver _terminology;
    private SummaryKey? _key;
    private ResourceSummary? _patient;
    private ResourceSummary? _record;
    private string? _release;
    // What the searches brought with their records (_include: the encounters of the rows)
    private readonly List<RelatedResource> _included = [];
    // Whether a type has a summary (an encounter opens as its summary): resolved with the rows, so the address of a row is known when it is drawn
    private readonly Dictionary<string, bool> _summaryTypes = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope.</param>
    public SummaryViewState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _credentials = serviceProvider.GetRequiredService<IServerCredentialStore>();
        _reader = serviceProvider.GetRequiredService<IFhirServerReader>();
        _types = serviceProvider.GetRequiredService<IServerResourceTypeProvider>();
        _views = serviceProvider.GetRequiredService<IResourceViews>();
        _tiles = serviceProvider.GetRequiredService<IResourceTiles>();
        _terminology = serviceProvider.GetRequiredService<ITerminologyResolver>();
        _terminology.Resolved += OnTerminologyResolved;
    }

    private string? TerminologyServer => _key?.Session.TerminologyServer;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _terminology.Resolved -= OnTerminologyResolved;
        }

        base.Dispose(disposing);
    }

    // The answers of the terminology server that came after the rows were made: the rows are made again with them
    private void OnTerminologyResolved(object? sender, EventArgs e)
    {
        UiErrors.Fire(async () =>
        {
            var sections = Sections;
            if (Server is null || sections.Count == 0)
            {
                return;
            }

            foreach (var section in sections)
            {
                section.Tiles.Clear();
                section.Cells.Clear();
                await RenderTilesAsync(sections, section);
                await RenderLabAsync(sections, section);
                await RenderTrendsAsync(section);
            }

            NotifyChanged();
        }, nameof(OnTerminologyResolved));
    }

    /// <summary>The server, once the state is activated.</summary>
    public IViewerServer? Server { get; private set; }

    /// <summary>The banners as their templates make them: the patient, then the record of the summary when it is not the patient; empty when the library has none.</summary>
    public string? Banner { get; private set; }

    /// <summary>The CSS of the kit fx-*.</summary>
    public string Style { get; private set; } = string.Empty;

    /// <summary>The blocks of the form the server can show.</summary>
    public IReadOnlyList<SummarySection> Sections { get; private set; } = [];

    /// <summary>The last load failed.</summary>
    public bool Failed { get; private set; }

    /// <summary>Every block that can be shown is open and has all its records: none is loading, none failed, none has a next page. A printout of the
    /// page is complete only then.</summary>
    public bool IsComplete => Sections.Where(section => !section.Hidden)
                                      .All(section => !section.Collapsed && section.IsLoaded && !section.IsLoading && !section.Failed && !section.HasMore);

    /// <summary>The records of every block are being loaded (<see cref="LoadAllAsync"/>).</summary>
    public bool LoadingAll { get; private set; }

    /// <summary>The last print was refused: not every record could be loaded.</summary>
    public bool PrintRefused { get; private set; }

    /// <summary>Whether the server can be read now: open, or with the credentials the user gave, or it needs them (Basic, Token), or it cannot be read yet (OAuth2).</summary>
    public ServerAccess Access
    {
        get
        {
            return Server is null ? ServerAccess.Ready : _credentials.AccessOf(Server);
        }
    }

    /// <summary>The server did not accept the credentials that were given: the form asks again.</summary>
    public bool CredentialsRejected { get; private set; }

    // The rows leave out what the banners show; a summary of an encounter keeps the rows that refer to it
    private string ContextKey => $"{_key!.ContextType}/{_key.ContextId}";

    private IReadOnlyList<string> Shown => _patient is null || _key!.ContextType == SummaryContexts.Patient ? [ContextKey] : [$"Patient/{_patient.Id}", ContextKey];

    /// <inheritdoc/>
    protected override async Task OnStartAsync(object? key)
    {
        _key = (SummaryKey)key!;
        await LoadAsync();
    }

    /// <summary>Loads the summary again.</summary>
    /// <returns>A task that completes when it is loaded.</returns>
    public async Task RetryAsync()
    {
        await LoadAsync();
    }

    /// <summary>The next pages of a block, or the first ones of the searches that failed.</summary>
    /// <param name="section">The block.</param>
    /// <returns>A task that completes when they are in.</returns>
    public Task LoadMoreAsync(SummarySection section)
    {
        return Fill(Sections, section);
    }

    /// <summary>
    /// Opens every block and loads all its records: the next pages, round after round, until no block has one (the searches that are under way are
    /// waited for). A block that fails stops there and shows its failure.
    /// </summary>
    /// <returns><see langword="true"/> when the page is complete (<see cref="IsComplete"/>).</returns>
    public async Task<bool> LoadAllAsync()
    {
        LoadingAll = true;
        SetCollapsed(false);
        try
        {
            var sections = Sections;
            for (var round = 0; round < MaxLoadAllRounds; round++)
            {
                await Task.WhenAll(_filling.Values.ToList());
                var todo = sections.Where(section => !section.Hidden && !section.Failed && (!section.IsLoaded || section.HasMore)).ToList();
                if (todo.Count == 0 || !ReferenceEquals(sections, Sections))
                {
                    break;
                }

                await Task.WhenAll(todo.Select(section => Fill(sections, section)));
            }

            return ReferenceEquals(sections, Sections) && IsComplete;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "Not every record of the summary could be loaded");
            return false;
        }
        finally
        {
            LoadingAll = false;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Loads every record before a print (<see cref="LoadAllAsync"/>); the print is refused when the page is not complete then.
    /// </summary>
    /// <returns><see langword="true"/> when the page may be printed.</returns>
    public async Task<bool> PrepareToPrintAsync()
    {
        PrintRefused = false;
        var complete = await LoadAllAsync();
        PrintRefused = !complete;
        NotifyChanged();
        return complete;
    }

    // A load of a block, remembered so that LoadAllAsync waits for the ones under way
    private Task Fill(IReadOnlyList<SummarySection> sections, SummarySection section)
    {
        var task = FillAsync(sections, section);
        _filling[section] = task;
        return task;
    }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            NotFound = false;
            Failed = false;
            CredentialsRejected = false;
            Banner = null;
            Sections = [];
            PrintRefused = false;
            _filling.Clear();
            _patient = null;
            _record = null;
            lock (_included)
            {
                _included.Clear();
            }

            var session = _key!.Session;
            var resolved = session.Server;
            Server = resolved;
            await ServiceProvider.GetRequiredService<ISmartAuthorizer>().RestoreAsync(resolved);

            if (Access != ServerAccess.Ready)
            {
                return;
            }

            try
            {
                _record = new ResourceSummary(_key.ContextId, _key.ContextType, await _reader.ReadAsync(resolved, _key.ContextType, _key.ContextId));
                _patient = _key.ContextType == SummaryContexts.Patient ? _record : await PatientOfAsync(resolved, _record);
                _release = await _types.ReleaseOfAsync(resolved);
                var form = await FormAsync();
                if (form is null)
                {
                    // No form grows from this record: its page instead
                    _key.Session.Navigate(new ViewerRoute(ViewerPage.Resource, _key.ContextType, _key.ContextId), replace: true);
                    return;
                }

                var banners = new System.Text.StringBuilder();
                foreach (var shown in new[] { _patient, _key.ContextType == SummaryContexts.Patient ? null : _record }.OfType<ResourceSummary>())
                {
                    var related = _patient is { Json: { } patient } && shown != _patient ? new[] { new RelatedResource(patient, null) } : [];
                    var view = await _views.RenderAsync(shown, related, _release, Link, IResourceViews.BannerMode, TerminologyServer);
                    banners.Append(view?.Html);
                    Style = view?.Style ?? Style;
                }

                Banner = banners.ToString();
                Sections = await SectionsOfAsync(resolved, form);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                NotFound = true;
            }
            catch (Exception ex) when (ex.IsRejected())
            {
                await ServiceProvider.GetRequiredService<ISmartAuthorizer>().DisconnectAsync(resolved);

                _credentials.Forget(resolved);
                CredentialsRejected = true;
            }
            catch
            {
                Failed = true;
                throw;
            }
        });
        NotifyChanged();
        if (Sections.Count > 0)
        {
            var sections = Sections;
            UiErrors.Fire(() => Task.WhenAll(sections.Select(section => Fill(sections, section))), nameof(FillAsync));
        }
    }

    // The form of the type of the record; none when the type has no summary
    private Task<SummaryFormDefinition?> FormAsync()
    {
        return ServiceProvider.GetRequiredService<ViewerSummaryForms>().FindAsync(_key!.ContextType);
    }

    // The patient of an encounter (its subject); none when it has no patient or the patient cannot be read (the summary still shows the encounter)
    private async Task<ResourceSummary?> PatientOfAsync(IViewerServer server, ResourceSummary record)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(record.Json!);
            if (document.RootElement.TryGetProperty("subject", out var subject) && subject.TryGetProperty("reference", out var reference)
                && reference.GetString()?.Split("/_history/", 2)[0].Split('/') is [.., "Patient", var id])
            {
                return new ResourceSummary(id, "Patient", await _reader.ReadAsync(server, "Patient", id));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            Logger.LogInformation(ex, "The patient of {Type} {Id} cannot be read, the summary shows the record alone", record.ResourceType, record.Id);
        }

        return null;
    }

    // The blocks of the form, without the searches of the types the server does not have; when it does not say what it has, all are tried
    private async Task<IReadOnlyList<SummarySection>> SectionsOfAsync(IViewerServer server, SummaryFormDefinition form)
    {
        IReadOnlyList<string>? declared = null;
        try
        {
            declared = (await _types.GetAsync(server)).Types;
        }
        catch (Exception ex) when (ex is HttpRequestException or Flox.Core.FloxException or System.Text.Json.JsonException)
        {
            Logger.LogInformation(ex, "The types of {Server} are not known, every section is tried", server.Name);
        }

        return SummarySections.Build(form, declared, System.Globalization.CultureInfo.CurrentUICulture.Name);
    }

    private async Task FillAsync(IReadOnlyList<SummarySection> sections, SummarySection section)
    {
        section.IsLoading = true;
        NotifyChanged();
        try
        {
            // A block that failed is retried where it failed; a block that is fine goes on to the next pages
            var retry = section.Failed;
            var todo = section.Searches.Where(search => !search.Unsupported && (!search.IsLoaded || search.Failed || (!retry && search.Next is not null))).ToList();
            var pages = await Task.WhenAll(todo.Select(search => PageOfAsync(section, search)));
            section.Add(pages.SelectMany(items => items));
            section.Failed = section.Searches.Any(search => search.Failed);
            await RenderTilesAsync(sections, section);
            await RenderLabAsync(sections, section);
            await RenderTrendsAsync(section);
        }
        finally
        {
            section.IsLoading = false;
            NotifyChanged();
        }
    }

    // observation-trends: the measures of the block; the first one is chosen and its history loaded
    private async Task RenderTrendsAsync(SummarySection section)
    {
        if (section.Component != SummaryComponents.ObservationTrends)
        {
            return;
        }

        section.Trends = ObservationTrends.Group(section.Items);
        if (section.SelectedTrend is null || section.Trends.All(trend => trend.Key != section.SelectedTrend))
        {
            section.SelectedTrend = section.Trends.Count > 0 ? section.Trends[0].Key : null;
        }

        await LoadTrendAsync(section);
    }

    /// <summary>Shows the chart of a measure of a block of <c>observation-trends</c>, loading its history the first time.</summary>
    /// <param name="section">The block.</param>
    /// <param name="key">The key of the measure.</param>
    /// <returns>The task.</returns>
    public async Task SelectTrendAsync(SummarySection section, string key)
    {
        section.SelectedTrend = key;
        NotifyChanged();
        await LoadTrendAsync(section);
    }

    // The history of the chosen measure: a search by its code (newest first); a server that refuses it shows the records of the block
    private async Task LoadTrendAsync(SummarySection section)
    {
        if (section.Trends.FirstOrDefault(trend => trend.Key == section.SelectedTrend) is not { } trend || section.TrendHistory.ContainsKey(trend.Key))
        {
            return;
        }

        IReadOnlyList<ResourceSummary> history = trend.Records;
        if (trend.Search is { } code && section.Searches.Count > 0)
        {
            section.TrendLoading = true;
            NotifyChanged();
            try
            {
                var query = section.Searches[0].HistoryQuery(_key!.ContextId, code, ObservationTrends.HistorySize, _key.ContextType == SummaryContexts.Patient ? null : _patient?.Id);
                var result = await _reader.SearchSortedAsync(Server!, "Observation", FhirSearchState.Parse(query), ObservationTrends.HistorySort, FhirNeeds.None);
                history = [.. Within(result.Page.Items).Concat(trend.Records).DistinctBy(item => item.Id)];
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogInformation(ex, "The history of {Code} is not available, the chart shows the records of the block", code);
            }
            finally
            {
                section.TrendLoading = false;
            }
        }

        section.TrendHistory[trend.Key] = history;
        NotifyChanged();
    }

    // lab-results: the results under their reports (the ones that came with the reports too), else by day; the results get their tiles (the narrow form)
    // and their cells (the partial lab-row), the ones that are in keep theirs
    private async Task RenderLabAsync(IReadOnlyList<SummarySection> sections, SummarySection section)
    {
        if (section.Component != SummaryComponents.LabResults)
        {
            return;
        }

        IReadOnlyList<ResourceSummary> included;
        lock (_included)
        {
            included = LabResults.ObservationsOf(_included);
        }

        var groups = LabResults.Group(section.Items, included, TimeZoneInfo.Local);
        var rows = groups.SelectMany(group => group.Rows).DistinctBy(SummarySection.KeyOf).ToList();
        var related = ContextOf(sections);
        var context = new TileContext(Shown, section.TileOptions, section.Format);
        var untiled = rows.Where(row => !section.Tiles.ContainsKey(SummarySection.KeyOf(row))).ToList();
        if (untiled.Count > 0)
        {
            var tiles = await _tiles.RenderAsync(untiled, _release, related, TerminologyServer, context, Server);
            foreach (var row in untiled)
            {
                section.Tiles[SummarySection.KeyOf(row)] = tiles.GetValueOrDefault(row.Id) ?? ResourceTiles.Plain(row);
            }
        }

        var fresh = rows.Where(row => !section.Cells.ContainsKey(SummarySection.KeyOf(row))).ToList();
        if (fresh.Count > 0)
        {
            var cells = await _tiles.RenderSlotsAsync(fresh, LabResults.RowTemplate, _release, related, TerminologyServer, context);
            foreach (var row in fresh)
            {
                section.Cells[SummarySection.KeyOf(row)] = cells.GetValueOrDefault(row.Id) ?? new Dictionary<string, string>();
            }
        }

        section.LabGroups = groups;
    }

    // The new records of the block get their rows; the ones that are in keep theirs
    private async Task RenderTilesAsync(IReadOnlyList<SummarySection> sections, SummarySection section)
    {
        var fresh = section.Items.Where(item => !section.Tiles.ContainsKey(SummarySection.KeyOf(item))).GroupBy(item => item.ResourceType).ToList();
        if (fresh.Count == 0)
        {
            return;
        }

        var related = ContextOf(sections);
        // the banners are above the blocks: the rows leave the patient (and the encounter) out
        var context = new TileContext(Shown, section.TileOptions, section.Format);
        foreach (var run in fresh)
        {
            if (!section.OpensInSheet(run.Key))
            {
                await HasSummaryAsync(run.Key);
            }

            var tiles = await _tiles.RenderAsync([.. run], _release, related, TerminologyServer, context, Server);
            foreach (var item in run)
            {
                section.Tiles[SummarySection.KeyOf(item)] = tiles.GetValueOrDefault(item.Id) ?? ResourceTiles.Plain(item);
            }
        }
    }

    /// <summary>Opens or closes a block.</summary>
    /// <param name="section">The block.</param>
    public void Toggle(SummarySection section)
    {
        section.Collapsed = !section.Collapsed;
        NotifyChanged();
    }

    /// <summary>Opens or closes every block.</summary>
    /// <param name="collapsed">Close them.</param>
    public void SetCollapsed(bool collapsed)
    {
        foreach (var section in Sections)
        {
            section.Collapsed = collapsed;
        }

        NotifyChanged();
    }

    /// <summary>Opens a block (the page then brings it to the top by its <see cref="SummarySection.ElementId"/>).</summary>
    /// <param name="section">The block.</param>
    public void OpenSection(SummarySection section)
    {
        if (section.Collapsed)
        {
            section.Collapsed = false;
            NotifyChanged();
        }
    }

    /// <summary>Opens a record of a section: in a sheet over the summary or on its page, as the section says.</summary>
    /// <param name="section">The section of the record.</param>
    /// <param name="item">The record.</param>
    /// <returns>A task that completes when the record is shown.</returns>
    public async Task OpenAsync(SummarySection section, ResourceSummary item)
    {
        var record = new ViewerRoute(ViewerPage.Resource, item.ResourceType, item.Id);
        if (section.OpensInSheet(item.ResourceType))
        {
            _key!.Session.OpenInSheet(record);
        }
        else
        {
            // A record whose type has a summary (an encounter) opens as its summary
            var summary = await HasSummaryAsync(item.ResourceType);
            _key!.Session.Navigate(summary ? new ViewerRoute(ViewerPage.Summary, item.ResourceType, item.Id) : record);
        }
    }

    /// <summary>
    /// The address of the place <see cref="OpenAsync"/> opens a record in: the summary with the record in a sheet over it, the summary of the record, or
    /// its page. A row of a section is a link to it, so it can be opened in another tab.
    /// </summary>
    /// <param name="section">The section of the record.</param>
    /// <param name="item">The record.</param>
    /// <returns>The <c>href</c> (<see cref="ViewerSession.HrefOf"/>); <see langword="null"/> before the page has started, or for an address the
    /// Viewer refuses (<see cref="ViewerRoutes.IsSafe"/>).</returns>
    /// <example>
    /// <code>
    /// &lt;Tile Model=@tile Href=@State.HrefOf(section, item) OnClick=@(() =&gt; State.OpenAsync(section, item))/&gt;
    /// </code>
    /// </example>
    public string? HrefOf(SummarySection section, ResourceSummary item)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(item);
        if (_key is null)
        {
            return null;
        }

        var record = new ViewerRoute(ViewerPage.Resource, item.ResourceType, item.Id);
        var route = section.OpensInSheet(item.ResourceType)
            ? new ViewerRoute(ViewerPage.Summary, _key.ContextType, _key.ContextId).WithPeek(record)
            : _summaryTypes.GetValueOrDefault(item.ResourceType) ? new ViewerRoute(ViewerPage.Summary, item.ResourceType, item.Id) : record;
        return ViewerRoutes.IsSafe(route.Location) ? _key.Session.HrefOf(route) : null;
    }

    private async Task<bool> HasSummaryAsync(string resourceType)
    {
        if (!_summaryTypes.TryGetValue(resourceType, out var summary))
        {
            summary = await ServiceProvider.GetRequiredService<ViewerSummaryForms>().HasFormAsync(resourceType);
            _summaryTypes[resourceType] = summary;
        }

        return summary;
    }

    // The records that refer to the record of the summary: a server may ignore a parameter it does not support (patient, encounter) and answer with the
    // records of other patients, which are never shown
    private IReadOnlyList<ResourceSummary> Within(IReadOnlyList<ResourceSummary> items)
    {
        var kept = items.Where(item => SummarySections.RefersTo(item.Json, ContextKey)).ToList();
        if (kept.Count < items.Count)
        {
            Logger.LogWarning("{Count} records that do not refer to {Context} are left out: the server ignored the parameter of the search", items.Count - kept.Count, ContextKey);
        }

        return kept;
    }

    // How many more pages one load asks for when a server that ignored the parameter of the record gave a page of other records only
    private const int MaxSkippedPages = 4;

    // One page of one search; a failure is the search's own, and a type the server does not have is no failure when the block may drop it
    private async Task<IReadOnlyList<ResourceSummary>> PageOfAsync(SummarySection section, SectionSearch search)
    {
        try
        {
            var page = search.IsLoaded ? await _reader.GetPageAsync(Server!, search.Next!, search.ResourceType) : await FirstPageAsync(section, search);
            var kept = Accept(search, page);
            for (var skipped = 0; kept.Count == 0 && page.Items.Count > 0 && page.Next is not null && skipped < MaxSkippedPages; skipped++)
            {
                page = await _reader.GetPageAsync(Server!, page.Next, search.ResourceType);
                kept = Accept(search, page);
            }

            search.IsLoaded = true;
            search.Failure = null;
            return kept;
        }
        catch (HttpRequestException ex) when (section.HideIfUnsupported && ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
        {
            Logger.LogInformation(ex, "The server does not search {Type}, the search is left out", search.ResourceType);
            search.Unsupported = true;
            return [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "The {Type} of {Context} cannot be loaded", search.ResourceType, _key is null ? null : ContextKey);
            search.Failure = SearchFailure.Of(ex, Server!);
            return [];
        }
    }

    // The records of a page that belong to the summary; the page brings its related resources and tells the next one
    private IReadOnlyList<ResourceSummary> Accept(SectionSearch search, ResourcePage page)
    {
        search.Next = page.Next;
        lock (_included)
        {
            _included.AddRange(page.Related ?? []);
        }

        return Within(page.Items);
    }

    // Newest first; a server that does not know the order gives its own (the reader asks again without it and remembers it for the server). A summary
    // of an encounter asks for its patient too; a server that refuses the parameter patient for the type is asked again without it
    private async Task<ResourcePage> FirstPageAsync(SummarySection section, SectionSearch search)
    {
        var needs = await NeedsAsync(section, search.ResourceType);
        var patient = _key!.ContextType == SummaryContexts.Patient ? null : _patient?.Id;
        try
        {
            var result = await _reader.SearchSortedAsync(Server!, search.ResourceType, FhirSearchState.Parse(search.FirstQuery(_key.ContextId, sorted: false, patient)), search.Sort, needs);
            search.ServerSorted = result.Sorted || search.Sort.Length == 0;
            return result.Page;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && patient is not null && !search.WithoutPatient)
        {
            Logger.LogInformation(ex, "The server refuses the parameter patient for {Type}, the records of the encounter are asked without it", search.ResourceType);
            search.WithoutPatient = true;
            return await FirstPageAsync(section, search);
        }
    }

    // What the templates of the rows need, and what the component needs: the reports of lab-results bring their results
    private async Task<FhirNeeds> NeedsAsync(SummarySection section, string resourceType)
    {
        var needs = await _tiles.NeedsAsync(resourceType);
        return section.Component == SummaryComponents.LabResults && resourceType == "DiagnosticReport" && !needs.Include.Contains(LabResults.ReportResults)
            ? needs with { Include = [.. needs.Include, LabResults.ReportResults] }
            : needs;
    }

    // What the rows may reach through a Reference: the patient, the record of the summary and whatever the other blocks have so far
    private List<RelatedResource> ContextOf(IReadOnlyList<SummarySection> sections)
    {
        var result = new List<RelatedResource>();
        foreach (var json in new[] { _patient?.Json, _record != _patient ? _record?.Json : null }.OfType<string>())
        {
            result.Add(new RelatedResource(json, null));
        }

        foreach (var item in sections.SelectMany(section => section.Items).Where(item => item.Json is not null))
        {
            result.Add(new RelatedResource(item.Json!, null));
        }

        lock (_included)
        {
            result.AddRange(_included);
        }

        return result;
    }

    private string? Link(string link)
    {
        var parts = link[FxHtml.FhirScheme.Length..].Split('/');
        return parts.Length == 2 && ViewerRoutes.Parse(new ViewerRoute(ViewerPage.Resource, parts[0], parts[1]).Location) is { } route ? _key!.Session.HrefOf(route) : null;
    }

    private sealed record SectionSpec(string ResourceType, string Sort, string SortedBy);
}
