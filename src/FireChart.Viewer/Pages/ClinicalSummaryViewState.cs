using System.Net;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Routing;
using Flox.Shared.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Pages;

/// <summary>
/// What the clinical summary shows.
/// </summary>
/// <param name="Session">The instance of the Viewer.</param>
/// <param name="PatientId">The id of the patient.</param>
public sealed record ClinicalSummaryKey(ViewerSession Session, string PatientId);

/// <summary>
/// The state of the clinical summary of a patient: the banner of the patient first, then the blocks of the form of the server, each from requests of
/// its own, all at once. Activated with a <see cref="ClinicalSummaryKey"/>.
/// </summary>
/// <example>
/// <code>
/// await state.ActivateAsync(new ClinicalSummaryKey(session, "123"));
/// var blocks = state.Sections;
/// </code>
/// </example>
public class ClinicalSummaryViewState : State
{
    private readonly IServerCredentialStore _credentials;
    private readonly IFhirServerReader _reader;
    private readonly IServerResourceTypeProvider _types;
    private readonly IResourceViews _views;
    private readonly IResourceTiles _tiles;
    private readonly ITerminologyResolver _terminology;
    private ClinicalSummaryKey? _key;
    private ResourceSummary? _patient;
    private string? _release;
    // What the searches brought with their records (_include: the encounters of the rows)
    private readonly List<RelatedResource> _included = [];

    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope.</param>
    public ClinicalSummaryViewState(IServiceProvider serviceProvider) : base(serviceProvider)
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

    /// <summary>The banner of the patient as its template makes it; empty when the library has none.</summary>
    public string? Banner { get; private set; }

    /// <summary>The CSS of the kit fx-*.</summary>
    public string Style { get; private set; } = string.Empty;

    /// <summary>The blocks of the form the server can show.</summary>
    public IReadOnlyList<ClinicalSummarySection> Sections { get; private set; } = [];

    /// <summary>The last load failed.</summary>
    public bool Failed { get; private set; }

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

    /// <summary>The id of the patient.</summary>
    public string PatientId
    {
        get
        {
            return _key?.PatientId ?? string.Empty;
        }
    }

    /// <inheritdoc/>
    protected override async Task OnStartAsync(object? key)
    {
        _key = (ClinicalSummaryKey)key!;
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
    public Task LoadMoreAsync(ClinicalSummarySection section)
    {
        return FillAsync(Sections, section);
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
            _patient = null;
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
                _patient = new ResourceSummary(_key.PatientId, "Patient", await _reader.ReadAsync(resolved, "Patient", _key.PatientId));
                _release = await _types.ReleaseOfAsync(resolved);
                var view = await _views.RenderAsync(_patient, [], _release, Link, IResourceViews.BannerMode, TerminologyServer);
                Banner = view?.Html ?? string.Empty;
                Style = view?.Style ?? string.Empty;
                Sections = await SectionsOfAsync(resolved);
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
            UiErrors.Fire(() => Task.WhenAll(sections.Select(section => FillAsync(sections, section))), nameof(FillAsync));
        }
    }

    // The blocks of the form of the server, without the searches of the types the server does not have; when it does not say what it has, all are tried
    private async Task<IReadOnlyList<ClinicalSummarySection>> SectionsOfAsync(IViewerServer server)
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

        var form = await ServiceProvider.GetRequiredService<ViewerSummaryForms>().GetAsync(_key!.Session.ClinicalSummaryForm);
        return ClinicalSummarySections.Build(form, declared, System.Globalization.CultureInfo.CurrentUICulture.Name);
    }

    private async Task FillAsync(IReadOnlyList<ClinicalSummarySection> sections, ClinicalSummarySection section)
    {
        section.IsLoading = true;
        NotifyChanged();
        try
        {
            // A block that failed is retried where it failed; a block that is fine goes on to the next pages
            var retry = section.Failed;
            var todo = section.Searches.Where(search => !search.Unsupported && (!search.IsLoaded || (!retry && search.Next is not null))).ToList();
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
    private async Task RenderTrendsAsync(ClinicalSummarySection section)
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
    public async Task SelectTrendAsync(ClinicalSummarySection section, string key)
    {
        section.SelectedTrend = key;
        NotifyChanged();
        await LoadTrendAsync(section);
    }

    // The history of the chosen measure: a search by its code (newest first); a server that refuses it shows the records of the block
    private async Task LoadTrendAsync(ClinicalSummarySection section)
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
                var query = section.Searches[0].HistoryQuery(_key!.PatientId, code, ObservationTrends.HistorySize);
                var page = await _reader.SearchAsync(Server!, "Observation", FhirSearchState.Parse(query), FhirNeeds.None);
                history = [.. page.Items.Concat(trend.Records).DistinctBy(item => item.Id)];
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
    private async Task RenderLabAsync(IReadOnlyList<ClinicalSummarySection> sections, ClinicalSummarySection section)
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
        var rows = groups.SelectMany(group => group.Rows).DistinctBy(ClinicalSummarySection.KeyOf).ToList();
        var related = ContextOf(sections);
        var context = new TileContext([$"Patient/{_key!.PatientId}"], section.TileOptions, section.Format);
        var untiled = rows.Where(row => !section.Tiles.ContainsKey(ClinicalSummarySection.KeyOf(row))).ToList();
        if (untiled.Count > 0)
        {
            var tiles = await _tiles.RenderAsync(untiled, _release, related, TerminologyServer, context, Server);
            foreach (var row in untiled)
            {
                section.Tiles[ClinicalSummarySection.KeyOf(row)] = tiles.GetValueOrDefault(row.Id) ?? ResourceTiles.Plain(row);
            }
        }

        var fresh = rows.Where(row => !section.Cells.ContainsKey(ClinicalSummarySection.KeyOf(row))).ToList();
        if (fresh.Count > 0)
        {
            var cells = await _tiles.RenderSlotsAsync(fresh, LabResults.RowTemplate, _release, related, TerminologyServer, context);
            foreach (var row in fresh)
            {
                section.Cells[ClinicalSummarySection.KeyOf(row)] = cells.GetValueOrDefault(row.Id) ?? new Dictionary<string, string>();
            }
        }

        section.LabGroups = groups;
    }

    // The new records of the block get their rows; the ones that are in keep theirs
    private async Task RenderTilesAsync(IReadOnlyList<ClinicalSummarySection> sections, ClinicalSummarySection section)
    {
        var fresh = section.Items.Where(item => !section.Tiles.ContainsKey(ClinicalSummarySection.KeyOf(item))).GroupBy(item => item.ResourceType).ToList();
        if (fresh.Count == 0)
        {
            return;
        }

        var related = ContextOf(sections);
        // the banner of the patient is above the blocks: the rows leave the patient out
        var context = new TileContext([$"Patient/{_key!.PatientId}"], section.TileOptions, section.Format);
        foreach (var run in fresh)
        {
            var tiles = await _tiles.RenderAsync([.. run], _release, related, TerminologyServer, context, Server);
            foreach (var item in run)
            {
                section.Tiles[ClinicalSummarySection.KeyOf(item)] = tiles.GetValueOrDefault(item.Id) ?? ResourceTiles.Plain(item);
            }
        }
    }

    /// <summary>Opens or closes a block.</summary>
    /// <param name="section">The block.</param>
    public void Toggle(ClinicalSummarySection section)
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

    /// <summary>Opens a block (the page then brings it to the top by its <see cref="ClinicalSummarySection.ElementId"/>).</summary>
    /// <param name="section">The block.</param>
    public void OpenSection(ClinicalSummarySection section)
    {
        if (section.Collapsed)
        {
            section.Collapsed = false;
            NotifyChanged();
        }
    }

    /// <summary>Goes to the page of a record.</summary>
    /// <param name="item">The record.</param>
    public void Open(ResourceSummary item)
    {
        _key!.Session.Navigate(new ViewerRoute(ViewerPage.Resource, item.ResourceType, item.Id));
    }

    // One page of one search; a failure is the search's own, and a type the server does not have is no failure when the block may drop it
    private async Task<IReadOnlyList<ResourceSummary>> PageOfAsync(ClinicalSummarySection section, SectionSearch search)
    {
        try
        {
            var page = search.IsLoaded ? await _reader.GetPageAsync(Server!, search.Next!, search.ResourceType) : await FirstPageAsync(section, search);
            search.Next = page.Next;
            search.IsLoaded = true;
            search.Failed = false;
            lock (_included)
            {
                _included.AddRange(page.Related ?? []);
            }

            return page.Items;
        }
        catch (HttpRequestException ex) when (section.HideIfUnsupported && ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
        {
            Logger.LogInformation(ex, "The server does not search {Type}, the search is left out", search.ResourceType);
            search.Unsupported = true;
            return [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "The {Type} of the patient {Patient} cannot be loaded", search.ResourceType, _key?.PatientId);
            search.Failed = true;
            return [];
        }
    }

    // Newest first; a server that does not know the order is asked again without it
    private async Task<ResourcePage> FirstPageAsync(ClinicalSummarySection section, SectionSearch search)
    {
        var needs = await NeedsAsync(section, search.ResourceType);
        try
        {
            return await _reader.SearchAsync(Server!, search.ResourceType, FhirSearchState.Parse(search.FirstQuery(_key!.PatientId, sorted: true)), needs);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && search.Sort.Length > 0)
        {
            search.ServerSorted = false;
            return await _reader.SearchAsync(Server!, search.ResourceType, FhirSearchState.Parse(search.FirstQuery(_key!.PatientId, sorted: false)), needs);
        }
    }

    // What the templates of the rows need, and what the component needs: the reports of lab-results bring their results
    private async Task<FhirNeeds> NeedsAsync(ClinicalSummarySection section, string resourceType)
    {
        var needs = await _tiles.NeedsAsync(resourceType);
        return section.Component == SummaryComponents.LabResults && resourceType == "DiagnosticReport" && !needs.Include.Contains(LabResults.ReportResults)
            ? needs with { Include = [.. needs.Include, LabResults.ReportResults] }
            : needs;
    }

    // What the rows may reach through a Reference: the patient and whatever the other blocks have so far
    private List<RelatedResource> ContextOf(IReadOnlyList<ClinicalSummarySection> sections)
    {
        var result = new List<RelatedResource>();
        if (_patient?.Json is { } patient)
        {
            result.Add(new RelatedResource(patient, null));
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
