namespace Flox.Shared.Pages.Diagnostics;

/// <summary>
/// A row of the demo list.
/// </summary>
/// <param name="number">The number of the row.</param>
public sealed class DemoRow(int number)
{
    /// <summary>The number of the row.</summary>
    public int Number { get; } = number;

    /// <summary>The name of the row.</summary>
    public string Name { get; } = $"Item {number}";

    /// <summary>The detail text of the row.</summary>
    public string Detail { get; } = $"Row {number} of the demo list";
}

/// <summary>A list state without a server: 95 rows in memory, paged and searched, with switches for a slow load, a failed load and no rows.</summary>
public sealed class DemoListState : IListState<DemoRow>
{
    private const int Size = 20;

    private readonly List<DemoRow> _small = [.. Enumerable.Range(1, 95).Select(i => new DemoRow(i))];
    private readonly List<DemoRow> _big = [.. Enumerable.Range(1, 5000).Select(i => new DemoRow(i))];
    private readonly HashSet<DemoRow> _selected = new HashSet<DemoRow>(ReferenceEqualityComparer.Instance);
    private List<DemoRow> _page = [];
    private long _total;
    private int _currentPage;
    private int _loads;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Makes the next load fail once.</summary>
    public bool FailNext { get; set; }

    /// <summary>Makes the list have no rows.</summary>
    public bool Empty { get; set; }

    /// <summary>Makes loads slow.</summary>
    public bool Slow { get; set; }

    /// <summary>Uses 5000 rows instead of 95, for the virtual list.</summary>
    public bool Big { get; set; }

    private List<DemoRow> All
    {
        get
        {
            return Big ? _big : _small;
        }
    }

    private List<DemoRow> Find()
    {
        return Empty ? [] : [.. All.Where(row => string.IsNullOrWhiteSpace(SearchText) || row.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))];
    }

    /// <inheritdoc />
    public IReadOnlyList<DemoRow> Items
    {
        get
        {
            return _page;
        }
    }

    /// <inheritdoc />
    public long Total
    {
        get
        {
            return _total;
        }
    }

    /// <inheritdoc />
    public int PageSize
    {
        get
        {
            return Size;
        }
    }

    /// <inheritdoc />
    public int CurrentPage
    {
        get
        {
            return _currentPage;
        }
    }

    /// <inheritdoc />
    public bool IsBusy { get; private set; }

    /// <inheritdoc />
    public Exception? LoadError { get; private set; }

    /// <inheritdoc />
    public string? SearchText { get; private set; }

    /// <inheritdoc />
    public bool CanSearch
    {
        get
        {
            return true;
        }
    }

    /// <inheritdoc />
    public DemoRow? Current { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<DemoRow> SelectedItems
    {
        get
        {
            return _selected;
        }
    }

    /// <inheritdoc />
    public Task SearchAsync(string? text)
    {
        SearchText = text;
        _currentPage = 0;
        return RefreshAsync();
    }

    /// <inheritdoc />
    public Task GoToPageAsync(int page)
    {
        _currentPage = page;
        return RefreshAsync();
    }

    /// <inheritdoc />
    public async Task RefreshAsync()
    {
        var load = ++_loads;
        IsBusy = true;
        LoadError = null;
        Changed?.Invoke(this, EventArgs.Empty);
        try
        {
            await Task.Delay(Slow ? 2000 : 300);
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("The demo load failed");
            }

            var found = Find();
            _total = found.Count;
            _page = [.. found.Skip(_currentPage * Size).Take(Size)];
        }
        catch (InvalidOperationException ex)
        {
            LoadError = ex;
        }
        finally
        {
            if (load == _loads)
            {
                IsBusy = false;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <inheritdoc />
    public void SetSearchText(string? text)
    {
        SearchText = text;
        _currentPage = 0;
        _selected.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task<ListRange<DemoRow>> LoadRangeAsync(int skip, int count, CancellationToken cancellationToken)
    {
        IsBusy = true;
        LoadError = null;
        Changed?.Invoke(this, EventArgs.Empty);
        try
        {
            await Task.Delay(Slow ? 2000 : 150, cancellationToken);
            if (FailNext)
            {
                FailNext = false;
                LoadError = new InvalidOperationException("The demo load failed");
                return new ListRange<DemoRow>([], 0);
            }

            var found = Find();
            return new ListRange<DemoRow>([.. found.Skip(skip).Take(count)], found.Count);
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Removes the rows from the demo data and reloads.</summary>
    public void Delete(IReadOnlyCollection<DemoRow> rows)
    {
        foreach (var row in rows)
        {
            _small.Remove(row);
            _big.Remove(row);
            _selected.Remove(row);
        }

        _ = RefreshAsync();
    }

    /// <inheritdoc />
    public bool IsSelected(DemoRow item)
    {
        return _selected.Contains(item);
    }

    /// <inheritdoc />
    public void ToggleSelected(DemoRow item)
    {
        if (!_selected.Remove(item))
        {
            _selected.Add(item);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void ClearSelection()
    {
        _selected.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public IReadOnlyList<DemoRow> Targets(DemoRow row)
    {
        return _selected.Contains(row) ? [.. _selected] : [row];
    }
}
