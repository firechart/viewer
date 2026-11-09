using System.Net;
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
    private TileOptions _tileOptions = TileOptions.Default;
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
                await RenderTilesAsync(sections, section);
            }

            Rebuild();
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

    /// <summary>The blocks laid out as one flat list of lines of one height: the header of a block, its cards, a line for loading, nothing, an error or "More".</summary>
    public IReadOnlyList<SummaryRow> Rows { get; private set; } = [];

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
            Rows = [];
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
                Rebuild();
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
        _tileOptions = new TileOptions(form.ShowAge, form.ShowStatus);
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
        }
        finally
        {
            section.IsLoading = false;
            Rebuild();
            NotifyChanged();
        }
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
        var context = new TileContext([$"Patient/{_key!.PatientId}"], _tileOptions);
        foreach (var run in fresh)
        {
            var tiles = await _tiles.RenderAsync([.. run], _release, related, TerminologyServer, context, Server);
            foreach (var item in run)
            {
                section.Tiles[ClinicalSummarySection.KeyOf(item)] = tiles.GetValueOrDefault(item.Id) ?? ResourceTiles.Plain(item);
            }
        }
    }

    private void Rebuild()
    {
        var rows = new List<SummaryRow>();
        foreach (var section in Sections.Where(section => !section.Hidden))
        {
            rows.Add(new SummaryRow(SummaryRowKind.Header, section));
            if (section.Collapsed)
            {
                continue;
            }

            if (!section.IsLoaded && !section.Failed)
            {
                rows.Add(new SummaryRow(SummaryRowKind.Spinner, section));
                continue;
            }

            if (section.IsLoaded && section.Items.Count == 0 && !section.Failed)
            {
                continue;
            }

            rows.AddRange(section.Items.Select(item => new SummaryRow(SummaryRowKind.Card, section, item, section.Tiles.GetValueOrDefault(ClinicalSummarySection.KeyOf(item)) ?? ResourceTiles.Plain(item))));
            if (section.Failed)
            {
                rows.Add(new SummaryRow(SummaryRowKind.Error, section));
            }
            else if (section.HasMore)
            {
                rows.Add(new SummaryRow(SummaryRowKind.More, section));
            }
        }

        Rows = rows;
    }

    /// <summary>Opens or closes a block.</summary>
    /// <param name="section">The block.</param>
    public void Toggle(ClinicalSummarySection section)
    {
        section.Collapsed = !section.Collapsed;
        Rebuild();
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

        Rebuild();
        NotifyChanged();
    }

    /// <summary>Opens a block.</summary>
    /// <param name="section">The block.</param>
    /// <returns>The index of its title row (the page scrolls to it); -1 when it is not on the page.</returns>
    public int OpenSection(ClinicalSummarySection section)
    {
        if (section.Collapsed)
        {
            section.Collapsed = false;
            Rebuild();
            NotifyChanged();
        }

        for (var index = 0; index < Rows.Count; index++)
        {
            if (Rows[index].Kind == SummaryRowKind.Header && ReferenceEquals(Rows[index].Section, section))
            {
                return index;
            }
        }

        return -1;
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
            var page = search.IsLoaded ? await _reader.GetPageAsync(Server!, search.Next!, search.ResourceType) : await FirstPageAsync(search);
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
    private async Task<ResourcePage> FirstPageAsync(SectionSearch search)
    {
        try
        {
            return await _reader.SearchAsync(Server!, search.ResourceType, FhirSearchState.Parse(search.FirstQuery(_key!.PatientId, sorted: true)), await _tiles.NeedsAsync(search.ResourceType));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && search.Sort.Length > 0)
        {
            search.ServerSorted = false;
            return await _reader.SearchAsync(Server!, search.ResourceType, FhirSearchState.Parse(search.FirstQuery(_key!.PatientId, sorted: false)), await _tiles.NeedsAsync(search.ResourceType));
        }
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
