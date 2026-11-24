using System.Diagnostics;
using Flox.Shared.Services;
using Flox.Shared.Settings;

namespace Flox.Shared.Components;

/// <summary>
/// State of a list over a data source: paging, search, filters, sorts, selection, optional bookmarks and deletion of rows.
/// </summary>
public abstract class DataSourceState<TKey, TEntity> : State,
                                                       IListState<TEntity>
where TKey : notnull
where TEntity : class
{
    private GeneralAppSettings? _appSettings;
    private readonly IAppSettingsProvider<GeneralAppSettings>? _appSettingsProvider;
    private CancellationTokenSource? _loadCts;
    private int _rangeLoads;

    /// <inheritdoc />
    protected DataSourceState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        DataSource = serviceProvider.GetRequiredService<IDataSource<TKey, TEntity>>();
        Selection = new SelectionModel<TKey, TEntity>(DataSource.GetKey);
        ItemComparer = new KeyComparer(DataSource.GetKey);
        _appSettingsProvider = serviceProvider.GetService<IAppSettingsProvider<GeneralAppSettings>>();
        _appSettingsProvider?.SettingsChanged += OnAppSettingsChanged;
        Selection.Changed += (_, _) => NotifyChanged();
        // Rows kept on the device (bookmarks, recent pages): another tab may change them
        if (serviceProvider.GetService<IStorage<TKey, TEntity>>() is ILocalChangeNotifier local)
        {
            _local = local;
            _local.ChangedElsewhere += OnChangedElsewhere;
        }
    }

    private readonly ILocalChangeNotifier? _local;

    /// <summary>
    /// Rows are the same row when their keys are equal: every range of a virtual list is loaded as new instances.
    /// </summary>
    public IEqualityComparer<TEntity> ItemComparer { get; }

    private sealed class KeyComparer(Func<TEntity, TKey> keyOf) : IEqualityComparer<TEntity>
    {
        public bool Equals(TEntity? x, TEntity? y)
        {
            return ReferenceEquals(x, y) || (x is not null && y is not null && EqualityComparer<TKey>.Default.Equals(keyOf(x), keyOf(y)));
        }

        public int GetHashCode(TEntity obj)
        {
            return keyOf(obj).GetHashCode();
        }
    }
    private bool _loaded;

    // Another tab changed the rows: a list that was shown reads them again
    private void OnChangedElsewhere(object? sender, EventArgs e)
    {
        if (_loaded)
        {
            _ = RefreshAsync();
        }
    }

    // The settings changed (the page size): read again when the list is opened next
    private void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        _appSettings = null;
    }

    /// <summary>
    /// Whether the list shows the bookmark of a row; off by default because it costs a request about every page.
    /// </summary>
    protected virtual bool BookmarksEnabled
    {
        get
        {
            return false;
        }
    }

    /// <summary>
    /// The error of the last load, or null; the rows are then the ones of the last good load of the same request (none after a start).
    /// </summary>
    public Exception? LoadError { get; private set; }

    /// <summary>
    /// The bookmark service of the entity, resolved on first use.
    /// </summary>
    public IUserBookmarkService<TKey, TEntity> BookmarkService
    {
        get
        {
            return field ??= ServiceProvider.GetRequiredService<IUserBookmarkService<TKey, TEntity>>();
        }
    }

    /// <summary>
    /// The selection of rows of the list.
    /// </summary>
    public SelectionModel<TKey, TEntity> Selection { get; }

    /// <summary>
    /// The full-text search string of the current request.
    /// </summary>
    public string? FullTextSearchString
    {
        get
        {
            return DataSource.Request.FullTextSearchString;
        }
    }

    /// <summary>
    /// The data source the list reads and writes.
    /// </summary>
    public IDataSource<TKey, TEntity> DataSource { get; }

    /// <summary>
    /// The filters of the current request.
    /// </summary>
    public FilterCollection Filters
    {
        get
        {
            return DataSource.Request.Filters;
        }
    }

    /// <summary>
    /// The sorts of the current request.
    /// </summary>
    public SortCollection Sorts
    {
        get
        {
            return DataSource.Request.Sorts;
        }
    }

    /// <summary>
    /// Bookmarks the row.
    /// </summary>
    public async Task AddBookmarkAsync(TEntity value)
    {
        await BookmarkService.AddBookmarkAsync(value);
        NotifyChanged();
    }

    /// <summary>
    /// Deletes the row with the key through the data source.
    /// </summary>
    public virtual async Task DeleteAsync(TKey key)
    {
        await DataSource.DeleteAsync(key);
    }

    /// <summary>
    /// Deletes the rows one by one and loads the list again, whatever came of it.
    /// </summary>
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

    /// <summary>
    /// Tells whether the row is bookmarked.
    /// </summary>
    public bool IsBookmarked(TEntity value)
    {
        return BookmarkService.IsBookmarked(value);
    }

    /// <summary>
    /// Reads the page size from the settings of the application and applies it to the data source.
    /// </summary>
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

    /// <summary>
    /// Called on a start after the request is reset: sets fields, sorts and the filters of the key.
    /// </summary>
    protected virtual void ConfigureRequest(object? key)
    {
    }

    /// <summary>
    /// Called before every load of the rows.
    /// </summary>
    protected virtual async Task BeforeLoadDataAsync()
    {
    }

    /// <summary>
    /// Called after every successful load of the rows.
    /// </summary>
    protected virtual async Task AfterLoadDataAsync()
    {
    }

    /// <inheritdoc />
    protected override async Task OnStartAsync(object? key)
    {
        DataSource.Reset();
        Selection.Clear();
        await ApplyPageSizeAsync();
        ConfigureRequest(key);
        await RefreshAsync();
    }

    /// <inheritdoc />
    protected override async Task OnResumeAsync()
    {
        await ApplyPageSizeAsync();
        await RefreshAsync();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task GoToPageAsync(int page)
    {
        Logger.LogDebug("List of {Entity}: go to page {Page}", typeof(TEntity).Name, page);
        DataSource.SetPage(page);
        await RefreshAsync();
    }

    /// <inheritdoc />
    public override bool RefreshesOnPull => true;

    /// <inheritdoc />
    public override Task OnPullRefreshAsync()
    {
        return RefreshAsync();
    }

    /// <summary>
    /// Loads the current request again; a newer load cancels the one still running.
    /// </summary>
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

            await AfterLoadDataAsync();
            _loaded = true;
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

    /// <summary>
    /// Removes the bookmark of the row.
    /// </summary>
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

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _appSettingsProvider?.SettingsChanged -= OnAppSettingsChanged;
            _local?.ChangedElsewhere -= OnChangedElsewhere;
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// State of a list over a data source of entities with a <see cref="Guid"/> key.
/// </summary>
/// <param name="serviceProvider">The service provider of the state.</param>
public class GuidDataSourceState<TEntity>
(
    IServiceProvider serviceProvider
) : DataSourceState<Guid, TEntity>(serviceProvider) where TEntity : class, IGuidEntity;
