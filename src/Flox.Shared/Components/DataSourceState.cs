using System.Diagnostics;
using Flox.Core.Replication;
using Flox.Shared.Services;
using Flox.Shared.Settings;

namespace Flox.Shared.Components;

public abstract class DataSourceState<TKey, TEntity> : State,
                                                       IListState<TEntity>
where TKey : notnull
where TEntity : class
{
    private GeneralAppSettings? _appSettings;
    private readonly IReplicationService? _replication;
    private Dictionary<string, ReplicationFailure> _failures = [];
    private readonly IAppSettingsProvider<GeneralAppSettings>? _appSettingsProvider;
    private CancellationTokenSource? _loadCts;
    private int _rangeLoads;

    protected DataSourceState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        DataSource = serviceProvider.GetRequiredService<IDataSource<TKey, TEntity>>();
        Selection = new SelectionModel<TKey, TEntity>(DataSource.GetKey);
        _appSettingsProvider = serviceProvider.GetService<IAppSettingsProvider<GeneralAppSettings>>();
        _appSettingsProvider?.SettingsChanged += OnAppSettingsChanged;
        Selection.Changed += (_, _) => NotifyChanged();
        if (ReplicatedType.IsReplicated(typeof(TEntity)) && serviceProvider.GetService<IReplicationService>() is { } replication)
        {
            _replication = replication;
            _replication.StateChanged += OnReplicationChanged;
        }
    }

    // The rows of a replicated type that the server did not accept, by the id of the document
    public string? FailureText(TEntity row)
    {
        var key = DataSource.GetKey(row);
        var id = key is Guid guid ? guid.ToString("D") : key.ToString();
        return id is not null && _failures.TryGetValue(id, out var failure) ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Sync_NotSynced, ReplicationFailureText.Describe(failure)) : null;
    }

    private async Task LoadFailuresAsync()
    {
        if (_replication is null)
        {
            return;
        }

        try
        {
            var type = ReplicatedType.Of(typeof(TEntity)).Name;
            _failures = (await _replication.GetFailuresAsync()).Where(row => string.Equals(row.Type, type, StringComparison.OrdinalIgnoreCase))
                                                                .GroupBy(row => row.DocumentId, StringComparer.OrdinalIgnoreCase)
                                                                .ToDictionary(row => row.Key, row => row.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "List of {Entity}: the refused changes could not be read", typeof(TEntity).Name);
        }
    }

    // A sync reports (a change was refused, or sent after an edit): the marks follow
    private void OnReplicationChanged(object? sender, EventArgs e)
    {
        _ = Task.Run(async () =>
        {
            await LoadFailuresAsync();
            NotifyChanged();
        });
    }

    // The settings changed (the page size): read again when the list is opened next
    private void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        _appSettings = null;
    }

    // A list shows the bookmark of a row only when its state says so: it costs a request about every page
    protected virtual bool BookmarksEnabled
    {
        get
        {
            return false;
        }
    }

    // The last load failed; the rows are the ones of the last good load of the same request (none after a start)
    public Exception? LoadError { get; private set; }

    // Resolved on first use: a list without bookmarks does not need the service
    public IUserBookmarkService<TKey, TEntity> BookmarkService
    {
        get
        {
            return field ??= ServiceProvider.GetRequiredService<IUserBookmarkService<TKey, TEntity>>();
        }
    }

    public SelectionModel<TKey, TEntity> Selection { get; }

    public string? FullTextSearchString
    {
        get
        {
            return DataSource.Request.FullTextSearchString;
        }
    }

    public IDataSource<TKey, TEntity> DataSource { get; }

    public FilterCollection Filters
    {
        get
        {
            return DataSource.Request.Filters;
        }
    }

    public SortCollection Sorts
    {
        get
        {
            return DataSource.Request.Sorts;
        }
    }

    public async Task AddBookmarkAsync(TEntity value)
    {
        await BookmarkService.AddBookmarkAsync(value);
        NotifyChanged();
    }

    public virtual async Task DeleteAsync(TKey key)
    {
        await DataSource.DeleteAsync(key);
    }

    // Deletes the rows one by one and loads the list again, whatever came of it
    public async Task DeleteRowsAsync(IReadOnlyCollection<TEntity> rows)
    {
        try
        {
            foreach (var row in rows)
            {
                await DataSource.Storage.DeleteAsync(DataSource.GetKey(row));
                Selection.Deselect(row);
            }
        }
        finally
        {
            await RefreshAsync();
        }
    }

    public bool IsBookmarked(TEntity value)
    {
        return BookmarkService.IsBookmarked(value);
    }

    protected virtual async Task ApplyPageSizeAsync()
    {
        if (_appSettings == null)
        {
            if (_appSettingsProvider != null)
            {
                _appSettings = await _appSettingsProvider.GetValue();
            }
        }

        if (_appSettings != null && DataSource.PageSize != _appSettings.PageSize)
        {
            DataSource.SetPageSize(_appSettings.PageSize);
            DataSource.SetPage(0);
        }
    }

    // Called on a start after the request is reset: fields, sorts and the filters of the key
    protected virtual void ConfigureRequest(object? key)
    {
    }

    protected virtual async Task BeforeLoadDataAsync()
    {
    }

    protected virtual async Task AfterLoadDataAsync()
    {
    }

    protected override async Task OnStartAsync(object? key)
    {
        DataSource.Reset();
        Selection.Clear();
        await ApplyPageSizeAsync();
        ConfigureRequest(key);
        await RefreshAsync();
    }

    protected override async Task OnResumeAsync()
    {
        await ApplyPageSizeAsync();
        await RefreshAsync();
    }

    public async Task SearchAsync(string? text)
    {
        if (string.Equals(FullTextSearchString, text, StringComparison.Ordinal))
        {
            return;
        }

        DataSource.Request.FullTextSearchString = text;
        DataSource.SetPage(0);
        Selection.Clear();
        await RefreshAsync();
    }

    public async Task GoToPageAsync(int page)
    {
        Logger.LogDebug("List of {Entity}: go to page {Page}", typeof(TEntity).Name, page);
        DataSource.SetPage(page);
        await RefreshAsync();
    }

    // Loads the current request again; a newer load cancels the one still running
    public async Task RefreshAsync()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _loadCts, cts);
        try
        {
            if (previous != null)
            {
                await previous.CancelAsync();
            }

            LoadError = null;
            IsBusy = true;

            await BeforeLoadDataAsync();

            // In healthcare, we never cache data
            var started = Stopwatch.GetTimestamp();
            await DataSource.LoadAsync(cts.Token);
            Logger.LogDebug("List of {Entity}: loaded skip {Skip} top {Top}, {Count} of {Total} rows in {Elapsed} ms",
                            typeof(TEntity).Name,
                            DataSource.Request.Skip,
                            DataSource.Request.Top,
                            DataSource.Items.Count,
                            DataSource.TotalItems,
                            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Selection.Refresh(DataSource.Items);
            if (BookmarksEnabled)
            {
                await BookmarkService.LoadBookmarksAsync(DataSource.Items);
            }

            await LoadFailuresAsync();
            await AfterLoadDataAsync();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Superseded by a newer load, which owns IsBusy now
        }
        catch (Exception ex)
        {
            LoadError = ex;
            Logger.LogWarning(ex, "List of {Entity}: the load failed", typeof(TEntity).Name);
            await this.HandleExceptionUI(ex: ex);
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                IsBusy = false;
            }
        }
    }

    public async Task RemoveBookmarkAsync(TEntity value)
    {
        await BookmarkService.RemoveBookmarkAsync(value);
        NotifyChanged();
    }


    void IListState<TEntity>.SetSearchText(string? text)
    {
        DataSource.Request.FullTextSearchString = text;
        DataSource.SetSkip(0);
        Selection.Clear();
        NotifyChanged();
    }

    // A request of its own for each range: the ranges of a virtual list are loaded in parallel
    async Task<ListRange<TEntity>> IListState<TEntity>.LoadRangeAsync(int skip, int count, CancellationToken cancellationToken)
    {
        var current = DataSource.Request;
        var request = new PagedRequest<TEntity>
        {
            Skip = skip,
            PageSize = count,
            ReturnTotal = true,
            FullTextSearchString = current.FullTextSearchString
        };
        request.Filters.AddRange(current.Filters);
        request.Sorts.AddRange(current.Sorts);
        request.Fields.AddRange(current.Fields);

        try
        {
            _rangeLoads++;
            LoadError = null;
            IsBusy = true;
            var page = await DataSource.Storage.FindAsync(request, cancellationToken);
            Selection.Refresh(page.Items);
            return new ListRange<TEntity>(page.Items, page.Total ?? page.Items.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LoadError = ex;
            await this.HandleExceptionUI(ex: ex);
            return new ListRange<TEntity>([], 0);
        }
        finally
        {
            _rangeLoads--;
            IsBusy = _rangeLoads > 0;
        }
    }

    IReadOnlyList<TEntity> IListState<TEntity>.Items
    {
        get
        {
            return DataSource.Items;
        }
    }

    long IListState<TEntity>.Total
    {
        get
        {
            return DataSource.TotalItems;
        }
    }

    int IListState<TEntity>.PageSize
    {
        get
        {
            return DataSource.PageSize;
        }
    }

    int IListState<TEntity>.CurrentPage
    {
        get
        {
            return DataSource.CurrentPage;
        }
    }

    string? IListState<TEntity>.SearchText
    {
        get
        {
            return FullTextSearchString;
        }
    }

    bool IListState<TEntity>.CanSearch
    {
        get
        {
            return true;
        }
    }

    TEntity? IListState<TEntity>.Current
    {
        get
        {
            return Selection.Current;
        }

        set
        {
            Selection.SetCurrent(value);
        }
    }

    IReadOnlyCollection<TEntity> IListState<TEntity>.SelectedItems
    {
        get
        {
            return Selection.Items;
        }
    }

    bool IListState<TEntity>.IsSelected(TEntity item)
    {
        return Selection.IsSelected(item);
    }

    void IListState<TEntity>.ToggleSelected(TEntity item)
    {
        Selection.Toggle(item);
    }

    void IListState<TEntity>.ClearSelection()
    {
        Selection.Clear();
    }

    IReadOnlyList<TEntity> IListState<TEntity>.Targets(TEntity row)
    {
        return Selection.Targets(row);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _appSettingsProvider?.SettingsChanged -= OnAppSettingsChanged;
            if (_replication is not null)
            {
                _replication.StateChanged -= OnReplicationChanged;
            }
        }

        base.Dispose(disposing);
    }
}

public class GuidDataSourceState<TEntity>
(
    IServiceProvider serviceProvider
) : DataSourceState<Guid, TEntity>(serviceProvider) where TEntity : class, IGuidEntity;
