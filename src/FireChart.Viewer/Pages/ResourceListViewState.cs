using System.Globalization;
using System.Net;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Routing;
using FireChart.Viewer.SearchForms;
using Flox.Shared.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Pages;

/// <summary>
/// What a list of resources shows: a type of a server with the search of the address.
/// </summary>
/// <param name="Session">The instance of the Viewer.</param>
/// <param name="ResourceType">The type.</param>
/// <param name="Query">The search: the query of the address, without the question mark.</param>
public sealed record ResourceListKey(ViewerSession Session, string ResourceType, string Query);

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
    private readonly IViewerStyle _style;
    private readonly FireChart.Viewer.Core.Reports.ReportLibrary _library;
    private readonly IListSearchMemory _memory;
    private readonly IValueSetExpander _expander;
    private readonly IReleaseModels _models;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _references = new(StringComparer.Ordinal);
    private bool _neverSearched;
    private CancellationTokenSource? _loading;
    private int _loads;
    private readonly List<RelatedResource> _related = [];
    private ResourceListKey? _key;

    // The rows of the page as the templates of the mode "tile" make them

    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope.</param>
    public ResourceListViewState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _credentials = serviceProvider.GetRequiredService<IServerCredentialStore>();
        _reader = serviceProvider.GetRequiredService<IFhirServerReader>();
        _types = serviceProvider.GetRequiredService<IServerResourceTypeProvider>();
        _style = serviceProvider.GetRequiredService<IViewerStyle>();
        _library = new FireChart.Viewer.Core.Reports.ReportLibrary(serviceProvider.GetRequiredService<IViewerAssets>(), FireChart.Viewer.Library.VendorLibrary.UrlOf);
        _memory = serviceProvider.GetRequiredService<IListSearchMemory>();
        _expander = serviceProvider.GetRequiredService<IValueSetExpander>();
        _models = serviceProvider.GetRequiredService<IReleaseModels>();
    }

    // The components of the library the tiles are drawn by: one for the page, so what the search read (the tile, its texts) is not read again
    internal FireChart.Viewer.Core.Reports.ReportLibrary Library => _library;

    /// <summary>The search form of the type; <see langword="null"/> when there is none, and then the list has no filter.</summary>
    public SearchFormDefinition? Form { get; private set; }

    /// <summary>The list has a search form.</summary>
    public bool CanFilter => Form is not null;

    /// <summary>What narrows the list now, one chip each (not <c>_count</c> and <c>_sort</c>).</summary>
    public IReadOnlyList<ActiveFilter> ActiveFilters =>
        [.. Search.Parameters.Where(row => row.Modifier is not null || row.Name is not (FhirSearchState.CountParameter or "_sort"))
                  .Select(row => new ActiveFilter(Caption(row), string.Join(", ", row.Values.Select(value => TextOf(row, value))), row))];

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

    /// <summary>
    /// The server refused the search (400, 422: a parameter it does not support, a value it cannot read): what it said, empty when it said nothing;
    /// <see langword="null"/> when it did not refuse. The list keeps the chips and the filter, so the search can be changed or cleared.
    /// </summary>
    public string? Rejection { get; private set; }

    /// <summary>The search of the address is not one; the list is not loaded.</summary>
    public bool InvalidSearch { get; private set; }

    /// <summary>The narrowing of the list: what the address holds.</summary>
    public FhirSearchState Search { get; private set; } = FhirSearchState.Empty;

    /// <summary>Whether the server can be read now.</summary>
    public ServerAccess Access => Server is null ? ServerAccess.Ready : _credentials.AccessOf(Server);

    /// <summary>The server did not accept the credentials that were given: the host asks again.</summary>
    public bool CredentialsRejected { get; private set; }

    /// <summary>What the searches of the list brought with their records (<c>_include</c>): their tiles read them from here, not from the server.</summary>
    public IReadOnlyList<RelatedResource> Related => _related;

    /// <summary>
    /// The same list without a filter; the other parameters stay.
    /// </summary>
    /// <param name="filter">The chip.</param>
    /// <returns>A task that completes when the search is kept and the address changed.</returns>
    public Task RemoveFilterAsync(ActiveFilter filter)
    {
        return GoAsync(Search.Replace([filter.Parameter], []));
    }

    /// <summary>
    /// The same list without any search: the cleared search is kept, so the list does not take the refused one again.
    /// </summary>
    /// <returns>A task that completes when the search is kept and the address changed.</returns>
    public Task ClearSearchAsync()
    {
        return GoAsync(FhirSearchState.Empty);
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
        var result = await SearchFormDialog.ShowAsync(DialogService, Form, Form.Title.Resolve(CultureInfo.CurrentUICulture.Name), read.Values, Server, _key!.Session);
        if (result is not null)
        {
            await GoAsync(SearchFormSearchMapping.Apply(Search, read, result.Conditions));
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
    /// The list is loaded again by a pull down at the top of a touch screen.
    /// </summary>
    public override bool RefreshesOnPull => true;

    /// <summary>
    /// Loads the list again from its first page with the same search, as <see cref="RetryAsync"/> does.
    /// </summary>
    /// <returns>A task that completes when the list is loaded.</returns>
    public override Task OnPullRefreshAsync()
    {
        return _key is null ? Task.CompletedTask : LoadAsync();
    }

    /// <summary>
    /// The most records a list reads, page by page as the user scrolls: then it tells to narrow the search with the filter.
    /// </summary>
    public const int MaxRecords = 500;

    /// <summary>
    /// The list has read <see cref="MaxRecords"/> and the server has more.
    /// </summary>
    public bool AtLimit => Items.Count >= MaxRecords && Page?.Next is not null;

    /// <summary>
    /// Adds the next page under the records that are shown (the list asks for it when its end comes into view); a failure keeps what is shown, and
    /// nothing is read past <see cref="MaxRecords"/>.
    /// </summary>
    /// <returns>A task that completes when the page is added.</returns>
    public async Task LoadMoreAsync()
    {
        if (string.IsNullOrEmpty(Page?.Next) || Server is null || LoadingMore || Items.Count >= MaxRecords || _loading is null)
        {
            return;
        }

        // The total, when the page had it, stays: the server may not repeat it on the next page. A page that comes after the list was loaded again (a
        // pull down, another search) is not added
        var total = Page.Total;
        var loading = _loading;
        LoadingMore = true;
        Rebuild();
        NotifyChanged();
        try
        {
            var next = await _reader.GetPageAsync(Server, Page.Next, _key!.ResourceType, loading.Token);
            if (ReferenceEquals(loading, _loading) && !loading.IsCancellationRequested)
            {
                await ShowAsync(next, total, append: true);
            }
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
            // the list was loaded again meanwhile
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
        _neverSearched = false;
        if (_key.Query.Length == 0)
        {
            var last = await _memory.GetAsync(_key.Session.Server, ListName);
            if (last is { Length: > 0 })
            {
                Navigate(last, true);
                return;
            }

            _neverSearched = last is null;
        }
        else
        {
            await _memory.SetAsync(_key.Session.Server, ListName, _key.Query);
        }

        await LoadAsync();
    }

    // The search is kept before the address changes: an empty one is a list the user cleared, not one to fill again
    private async Task GoAsync(FhirSearchState search, bool replace = false)
    {
        var query = search.ToQueryString();
        await _memory.SetAsync(_key!.Session.Server, ListName, query);
        Navigate(query, replace);
    }

    private void Navigate(string query, bool replace)
    {
        _key!.Session.Navigate(new ViewerRoute(ViewerPage.ResourceList, _key.ResourceType, Query: query), replace);
    }

    private string ListName => _key!.ResourceType;

    private string Caption(FhirSearchParameter parameter)
    {
        var name = NameOf(parameter);
        return FieldOf(parameter) is { } field && field.Label.Resolve(CultureInfo.CurrentUICulture.Name) is { Length: > 0 } label ? label : name;
    }

    private static string NameOf(FhirSearchParameter parameter)
    {
        return parameter.Modifier is null ? parameter.Name : $"{parameter.Name}:{parameter.Modifier}";
    }

    private SearchFormField? FieldOf(FhirSearchParameter parameter)
    {
        var name = NameOf(parameter);
        return Form?.Fields.Where(row => name == row.QueryName || name.StartsWith(row.QueryName + ":", StringComparison.Ordinal))
                   .OrderByDescending(row => row.QueryName.Length)
                   .FirstOrDefault();
    }

    // A code of the value set of its field reads as its display (Final, not http://hl7.org/fhir/observation-status|final) once the server expanded it;
    // a reference as its record (Mr. Adolph Smith, not Patient/d67a…) once it is read
    private string TextOf(FhirSearchParameter parameter, string value)
    {
        if (_references.TryGetValue(value, out var record))
        {
            return record;
        }

        return Server is not null && FieldOf(parameter) is { ValueSetUrl: { Length: > 0 } valueSet } field
               && _expander.DisplayOf(Server, valueSet, field.ValueSetVersion, value) is { Length: > 0 } display
            ? display
            : FhirSearchQuery.Unescape(value);
    }

    // The records the reference fields of the search name (subject=Patient/123, patient=123), read for their line of text
    private async Task DescribeReferencesAsync(IViewerServer server, CancellationToken cancellationToken)
    {
        var references = Search.Parameters
                               .Where(row => row.Modifier is null && FieldOf(row) is { Control: SearchFieldControl.Reference })
                               .SelectMany(row => row.Values.Select(value => (Value: value, Reference: ReferenceOf(FieldOf(row)!, FhirSearchQuery.Unescape(value)))))
                               .Where(pair => pair.Reference is not null && !_references.ContainsKey(pair.Value))
                               .ToList();
        if (references.Count == 0)
        {
            return;
        }

        var model = await _models.GetAsync(await _types.ReleaseOfAsync(server), cancellationToken);
        // Read together: the page waits for its chips
        await Task.WhenAll(references.Select(async pair =>
        {
            try
            {
                var parts = pair.Reference!.Split('/');
                var json = await _reader.ReadAsync(server, parts[0], parts[1], cancellationToken);
                var text = FireChart.Viewer.Core.Reports.ReportText.TitleOf(FireChart.Viewer.Core.Reports.ReportExpressions.Parse(json, model), CultureInfo.CurrentCulture);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _references[pair.Value] = text;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
            {
                // the chip keeps the reference
            }
        }));

        NotifyChanged();
    }

    // A reference of a field, or the id alone of a record of its one type
    private static string? ReferenceOf(SearchFormField field, string value)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Z][A-Za-z]{0,63}/[A-Za-z0-9\-.]{1,64}$"))
        {
            return value;
        }

        return field.Lookup?.TargetTypes is [var type] && System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Za-z0-9\-.]{1,64}$") ? $"{type}/{value}" : null;
    }

    // The value sets of the coded fields the search uses are expanded (once per server), so the chips show the displays of their codes
    private async Task DescribeFiltersAsync(IViewerServer server)
    {
        var valueSets = Search.Parameters.Select(FieldOf)
                              .OfType<SearchFormField>()
                              .Where(f => f.ValueSetUrl is { Length: > 0 } && f.Control is SearchFieldControl.Select or SearchFieldControl.MultiSelect or SearchFieldControl.Autocomplete)
                              .DistinctBy(f => (f.ValueSetUrl, f.ValueSetVersion))
                              .ToList();
        if (valueSets.Count == 0)
        {
            return;
        }

        await Task.WhenAll(valueSets.Select(field => _expander.ExpandAsync(server, field.ValueSetUrl!, field.ValueSetVersion, null)));
        NotifyChanged();
    }

    // A chip that cannot be described keeps the value as it is: the list does not fail for it
    private async Task QuietlyAsync(Task describing)
    {
        try
        {
            await describing;
        }
        catch (OperationCanceledException)
        {
            // of a load that a newer one replaced
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "A filter of the list of {Type} is not described", _key!.ResourceType);
        }
    }

    // Each load starts from the first page: the position in the list is not kept. A load that a newer one replaced (the user went on before it
    // ended) is cancelled and writes nothing: the page shows the newer one only, and its indicator stays until the newer one ends
    private async Task LoadAsync()
    {
        var session = _key!.Session;
        var key = _key;
        if (_loading is not null)
        {
            await _loading.CancelAsync();
            _loading.Dispose();
        }

        var loading = _loading = new CancellationTokenSource();
        var cancellationToken = loading.Token;
        bool Current()
        {
            return ReferenceEquals(_key, key) && !cancellationToken.IsCancellationRequested;
        }

        Interlocked.Increment(ref _loads);
        await RunAsync(async () =>
        {
            Failed = false;
            Rejection = null;
            CredentialsRejected = false;
            InvalidSearch = false;
            Form = null;
            Items = [];
            _related.Clear();
            Rows = [];
            Page = null;
            Server = session.Server;
            // The sheet of the kit is in the head before the first rows (KitStyleSheet)
            if (Style.Length == 0)
            {
                Style = await _style.GetStyleAsync();
                await KitStyleSheet.AdoptAsync(ServiceProvider.GetRequiredService<IJSRuntime>(), Style);
            }

            await ServiceProvider.GetRequiredService<ISmartAuthorizer>().RestoreAsync(session.Server);

            if (!Current() || Access != ServerAccess.Ready)
            {
                return;
            }

            try
            {
                Search = FhirSearchState.Parse(key.Query);
            }
            catch (FhirSearchException)
            {
                Search = FhirSearchState.Empty;
                InvalidSearch = true;
                return;
            }

            // The form and the texts of the tiles are read while the server searches; the chips of the filters are described before the page shows, so
            // it appears whole
            var form = FormOfAsync(session.Server, key.ResourceType);
            // (the library keeps what it reads for every later caller: it is not given the token of this load)
            var texts = _library.StringsAsync(CultureInfo.CurrentUICulture);
            try
            {
                var needs = await FireChart.Viewer.Core.Reports.ReportNeeds.OfTileAsync(_library, key.ResourceType);
                var found = await _reader.SearchAsync(session.Server, key.ResourceType, Search, needs, cancellationToken);
                if (!Current())
                {
                    return;
                }

                ApplyPage(found, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex.IsRejected())
            {
                await ServiceProvider.GetRequiredService<ISmartAuthorizer>().DisconnectAsync(session.Server);

                _credentials.Forget(session.Server);
                CredentialsRejected = true;
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            {
                if (!Current())
                {
                    return;
                }

                Logger.LogInformation("The server refused the search of {Type}: {Outcome}", key.ResourceType, (ex as FhirRequestException)?.Outcome ?? ex.Message);
                Rejection = (ex as FhirRequestException)?.Outcome ?? string.Empty;
            }
            catch
            {
                Failed = true;
                throw;
            }

            if (Access == ServerAccess.Ready)
            {
                var read = await form;
                await QuietlyAsync(texts);
                if (!Current())
                {
                    return;
                }

                Form = read;
                await Task.WhenAll(QuietlyAsync(DescribeFiltersAsync(session.Server)), QuietlyAsync(DescribeReferencesAsync(session.Server, cancellationToken)));
                if (Current() && _neverSearched && Form is not null && SearchFormSearchMapping.Defaults(Form) is { Parameters.Count: > 0 } defaults)
                {
                    await GoAsync(defaults, true);
                }
            }
        });

        // A load replaced that ends while the newer one runs: its RunAsync has cleared the indicator the newer one still needs
        if (Interlocked.Decrement(ref _loads) > 0)
        {
            IsBusy = true;
            return;
        }

        NotifyChanged();
    }

    // The form is an addition: without it (no form anywhere, the server of the host does not answer) the list works without a filter
    private async Task<SearchFormDefinition?> FormOfAsync(IViewerServer server, string resourceType)
    {
        try
        {
            if (await _types.ReleaseOfAsync(server) is { Length: > 0 } release)
            {
                return await ServiceProvider.GetRequiredService<ViewerSearchForms>().FindAsync(new SearchFormKey(release.ToLowerInvariant(), resourceType));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogDebug(ex, "The search form of {Type} is not available", resourceType);
        }

        return null;
    }

    private async Task ShowAsync(ResourcePage page, long? previousTotal, bool append = false)
    {
        if (Style.Length == 0)
        {
            Style = await _style.GetStyleAsync();
            await KitStyleSheet.AdoptAsync(ServiceProvider.GetRequiredService<IJSRuntime>(), Style);
        }

        ApplyPage(page, previousTotal, append);
    }

    private void ApplyPage(ResourcePage page, long? previousTotal, bool append = false)
    {
        Page = page.Total is null && previousTotal is not null ? page with { Total = previousTotal } : page;
        _related.AddRange(page.Related ?? []);
        Items = append ? [.. Items, .. page.Items] : page.Items;
        Rebuild();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loading?.Cancel();
            _loading?.Dispose();
            _loading = null;
        }

        base.Dispose(disposing);
    }

    private void Rebuild()
    {
        var rows = new List<ListRow>();
        rows.AddRange(Items.Select(item => new ListRow(ListRowKind.Card, item)));
        if (rows.Count == 0)
        {
            rows.Add(new ListRow(ListRowKind.Empty));
        }
        else if (AtLimit)
        {
            rows.Add(new ListRow(ListRowKind.Limit));
        }
        else if (Page?.Next is not null && LoadingMore)
        {
            // One tile in the place of More while the next page is read: the list keeps its height until the page comes, so its scroll bar moves once
            rows.Add(new ListRow(ListRowKind.Loading));
        }
        else if (Page?.Next is not null)
        {
            rows.Add(new ListRow(ListRowKind.More));
        }

        Rows = rows;
    }
}
