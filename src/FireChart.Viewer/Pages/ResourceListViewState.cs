using System.Globalization;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Routing;
using FireChart.Viewer.SearchForms;
using Flox.Shared.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Pages;

/// <summary>
/// What a list of resources shows: a type of a server with the search of the address.
/// </summary>
/// <param name="Session">The instance of the Viewer.</param>
/// <param name="ResourceType">The type.</param>
/// <param name="Query">The search: the query of the address, without the question mark.</param>
/// <param name="ClinicalSummaryForm">The list is the one of the patients that leads to the clinical summary of the one that is chosen.</param>
public sealed record ResourceListKey(ViewerSession Session, string ResourceType, string Query, bool ClinicalSummaryForm = false);

/// <summary>
/// A parameter of the search of a list as a chip.
/// </summary>
/// <param name="Caption">The label of the field of the form, else the name of the parameter.</param>
/// <param name="Text">The values, unescaped.</param>
/// <param name="Parameter">The parameter, to take it out of the search.</param>
public sealed record ActiveFilter(string Caption, string Text, FhirSearchParameter Parameter);

/// <summary>
/// The state of a list of resources: the pages of a search as tiles, the search form of the type, the count. Activated with a <see cref="ResourceListKey"/>.
/// </summary>
/// <example>
/// <code>
/// await state.ActivateAsync(new ResourceListKey(session, "Patient", "family=Smith"));
/// var rows = state.Rows;
/// </code>
/// </example>
public class ResourceListViewState : State
{
    private readonly IServerCredentialStore _credentials;
    private readonly IFhirServerReader _reader;
    private readonly IServerResourceTypeProvider _types;
    private readonly IResourceTiles _tiles;
    private readonly ITerminologyResolver _terminology;
    private readonly List<RelatedResource> _related = [];
    private ResourceListKey? _key;

    // The rows of the page as the templates of the mode "tile" make them
    private IReadOnlyDictionary<string, TileModel> _tileRows = new Dictionary<string, TileModel>();

    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope.</param>
    public ResourceListViewState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _credentials = serviceProvider.GetRequiredService<IServerCredentialStore>();
        _reader = serviceProvider.GetRequiredService<IFhirServerReader>();
        _types = serviceProvider.GetRequiredService<IServerResourceTypeProvider>();
        _tiles = serviceProvider.GetRequiredService<IResourceTiles>();
        _terminology = serviceProvider.GetRequiredService<ITerminologyResolver>();
        _terminology.Resolved += OnTerminologyResolved;
    }

    /// <summary>The search form of the type; <see langword="null"/> when there is none, and then the list has no filter.</summary>
    public SearchFormDefinition? Form { get; private set; }

    /// <summary>The list has a search form.</summary>
    public bool CanFilter => Form is not null;

    /// <summary>What narrows the list now, one chip each (not <c>_count</c> and <c>_sort</c>).</summary>
    public IReadOnlyList<ActiveFilter> ActiveFilters =>
        [.. Search.Parameters.Where(row => row.Modifier is not null || row.Name is not (FhirSearchState.CountParameter or "_sort"))
                  .Select(row => new ActiveFilter(Caption(row), string.Join(", ", row.Values.Select(FhirSearchQuery.Unescape)), row))];

    /// <summary>How many parameters of the search the form shows.</summary>
    public int FilterCount => Form is null ? 0 : SearchFormSearchMapping.Read(Form, Search).Consumed.Count;

    /// <summary>The server, once the state is activated.</summary>
    public IViewerServer? Server { get; private set; }

    /// <summary>Every record loaded so far: a next page is added to them.</summary>
    public IReadOnlyList<ResourceSummary> Items { get; private set; } = [];

    /// <summary>The lines of the list: the records as tiles, a note that there is none, the line for the next page.</summary>
    public IReadOnlyList<ListRow> Rows { get; private set; } = [];

    /// <summary>The next page is being loaded.</summary>
    public bool LoadingMore { get; private set; }

    /// <summary>The CSS of the kit <c>fx-*</c>: the rows are its list rows.</summary>
    public string Style { get; private set; } = string.Empty;

    /// <summary>What the server said about the page: the links of the <c>Bundle</c> and, if it counts, the total.</summary>
    public ResourcePage? Page { get; private set; }

    /// <summary>The last load failed.</summary>
    public bool Failed { get; private set; }

    /// <summary>The search of the address is not one; the list is not loaded.</summary>
    public bool InvalidSearch { get; private set; }

    /// <summary>The narrowing of the list: what the address holds.</summary>
    public FhirSearchState Search { get; private set; } = FhirSearchState.Empty;

    /// <summary>The count is being asked for; the list stays as it is.</summary>
    public bool Counting { get; private set; }

    /// <summary>Whether the server can be read now.</summary>
    public ServerAccess Access => Server is null ? ServerAccess.Ready : _credentials.AccessOf(Server);

    /// <summary>The server did not accept the credentials that were given: the host asks again.</summary>
    public bool CredentialsRejected { get; private set; }

    /// <summary>
    /// The row of a record: its tile, else its id and type.
    /// </summary>
    /// <param name="item">The record.</param>
    /// <returns>The tile.</returns>
    public TileModel TileOf(ResourceSummary item)
    {
        return _tileRows.GetValueOrDefault(item.Id) ?? ResourceTiles.Plain(item);
    }

    /// <summary>
    /// The same list without a filter; the other parameters stay.
    /// </summary>
    /// <param name="filter">The chip.</param>
    public void RemoveFilter(ActiveFilter filter)
    {
        Go(Search.Replace([filter.Parameter], []));
    }

    /// <summary>
    /// Opens the search form with the values of the search; what the user sets goes to the address, and the list loads again from there.
    /// </summary>
    /// <returns>A task that completes when the form is closed.</returns>
    public async Task OpenFilterAsync()
    {
        if (Form is null || Server is null)
        {
            return;
        }

        var read = SearchFormSearchMapping.Read(Form, Search);
        var result = await SearchFormDialog.ShowAsync(DialogService, Form, Form.Title.Resolve(CultureInfo.CurrentUICulture.Name), read.Values);
        if (result is not null)
        {
            Go(SearchFormSearchMapping.Apply(Search, read, result.Conditions));
        }
    }

    /// <summary>
    /// Asks the server how many records the search finds, by a request of its own; a slow count only keeps its button busy.
    /// </summary>
    /// <returns>A task that completes when the count is known.</returns>
    public async Task ShowTotalAsync()
    {
        if (Server is null || Page is null || Counting)
        {
            return;
        }

        Counting = true;
        NotifyChanged();
        try
        {
            if (await _reader.CountAsync(Server, _key!.ResourceType, Search) is { } total)
            {
                Page = Page with { Total = total };
            }
        }
        catch (Exception ex)
        {
            await UiErrors.HandleAsync(ex, nameof(ShowTotalAsync));
        }
        finally
        {
            Counting = false;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Loads the list again from its first page.
    /// </summary>
    /// <returns>A task that completes when it is loaded.</returns>
    public async Task RetryAsync()
    {
        await LoadAsync();
    }

    /// <summary>
    /// Adds the next page under the records that are shown; a failure keeps what is shown.
    /// </summary>
    /// <returns>A task that completes when the page is added.</returns>
    public async Task LoadMoreAsync()
    {
        if (string.IsNullOrEmpty(Page?.Next) || Server is null || LoadingMore)
        {
            return;
        }

        // The total, when the page had it, stays: the server may not repeat it on the next page
        var total = Page.Total;
        LoadingMore = true;
        Rebuild();
        NotifyChanged();
        try
        {
            await ShowAsync(await _reader.GetPageAsync(Server, Page.Next, _key!.ResourceType), total, append: true);
        }
        catch (Exception ex)
        {
            await UiErrors.HandleAsync(ex, nameof(LoadMoreAsync));
        }
        finally
        {
            LoadingMore = false;
            Rebuild();
            NotifyChanged();
        }
    }

    /// <inheritdoc/>
    protected override async Task OnStartAsync(object? key)
    {
        _key = (ResourceListKey)key!;
        await LoadAsync();
    }

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
            if (Server is null || Items.Count == 0)
            {
                return;
            }

            _tileRows = await _tiles.RenderAsync(Items, await _types.ReleaseOfAsync(Server), _related, _key!.Session.TerminologyServer, server: Server);
            Rebuild();
            NotifyChanged();
        }, nameof(OnTerminologyResolved));
    }

    private void Go(FhirSearchState search)
    {
        var page = _key!.ClinicalSummaryForm ? new ViewerRoute(ViewerPage.ClinicalSummary) : new ViewerRoute(ViewerPage.ResourceList, _key.ResourceType);
        _key.Session.Navigate(page with { Query = search.ToQueryString() });
    }

    private string Caption(FhirSearchParameter parameter)
    {
        var name = parameter.Modifier is null ? parameter.Name : $"{parameter.Name}:{parameter.Modifier}";
        var field = Form?.Fields.Where(row => name == row.QueryName || name.StartsWith(row.QueryName + ":", StringComparison.Ordinal))
                         .OrderByDescending(row => row.QueryName.Length)
                         .FirstOrDefault();
        return field is null ? name : field.Label.Resolve(CultureInfo.CurrentUICulture.Name) is { Length: > 0 } label ? label : name;
    }

    // Each load starts from the first page: the position in the list is not kept
    private async Task LoadAsync()
    {
        var session = _key!.Session;
        await RunAsync(async () =>
        {
            Failed = false;
            CredentialsRejected = false;
            InvalidSearch = false;
            Form = null;
            Items = [];
            _related.Clear();
            Rows = [];
            _tileRows = new Dictionary<string, TileModel>();
            Page = null;
            Server = session.Server;
            await ServiceProvider.GetRequiredService<ISmartAuthorizer>().RestoreAsync(session.Server);

            if (Access != ServerAccess.Ready)
            {
                return;
            }

            try
            {
                Search = FhirSearchState.Parse(_key.Query);
            }
            catch (FhirSearchException)
            {
                Search = FhirSearchState.Empty;
                InvalidSearch = true;
                return;
            }

            try
            {
                await ShowAsync(await _reader.SearchAsync(session.Server, _key.ResourceType, Search, await _tiles.NeedsAsync(_key.ResourceType)), null);
            }
            catch (Exception ex) when (ex.IsRejected())
            {
                await ServiceProvider.GetRequiredService<ISmartAuthorizer>().DisconnectAsync(session.Server);

                _credentials.Forget(session.Server);
                CredentialsRejected = true;
            }
            catch
            {
                Failed = true;
                throw;
            }

            if (Access == ServerAccess.Ready)
            {
                await LoadFormAsync(session.Server);
            }
        });
        NotifyChanged();
    }

    // The form is an addition: without it (no form anywhere, the server of the host does not answer) the list works without a filter
    private async Task LoadFormAsync(IViewerServer server)
    {
        try
        {
            if (await _types.ReleaseOfAsync(server) is { Length: > 0 } release)
            {
                Form = await ServiceProvider.GetRequiredService<ViewerSearchForms>().FindAsync(new SearchFormKey(release.ToLowerInvariant(), _key!.ResourceType));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogDebug(ex, "The search form of {Type} is not available", _key!.ResourceType);
        }
    }

    private async Task ShowAsync(ResourcePage page, long? previousTotal, bool append = false)
    {
        Page = page.Total is null && previousTotal is not null ? page with { Total = previousTotal } : page;
        _related.AddRange(page.Related ?? []);
        var release = Server is null ? null : await _types.ReleaseOfAsync(Server);
        var tiles = await _tiles.RenderAsync(page.Items, release, page.Related, _key!.Session.TerminologyServer, server: Server);
        _tileRows = append ? new Dictionary<string, TileModel>(_tileRows.Concat(tiles), StringComparer.Ordinal) : tiles;
        Style = await _tiles.StyleAsync();
        Items = append ? [.. Items, .. page.Items] : page.Items;
        Rebuild();
    }

    private void Rebuild()
    {
        var rows = new List<ListRow>();
        rows.AddRange(Items.Select(item => new ListRow(ListRowKind.Card, item, TileOf(item))));
        if (rows.Count == 0)
        {
            rows.Add(new ListRow(ListRowKind.Empty));
        }
        else if (Page?.Next is not null)
        {
            rows.Add(new ListRow(ListRowKind.More));
        }

        Rows = rows;
    }
}
