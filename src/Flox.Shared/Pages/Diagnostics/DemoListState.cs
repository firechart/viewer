namespace Flox.Shared.Pages.Diagnostics;

public sealed class DemoRow(int number)
{
    public int Number { get; } = number;

    public string Name { get; } = $"Item {number}";

    public string Detail { get; } = $"Row {number} of the demo list";
}

// A list state without a server: 95 rows in memory, paged and searched, with switches for a slow load, a failed load and no rows
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

    public event EventHandler? Changed;

    public bool FailNext { get; set; }

    public bool Empty { get; set; }

    public bool Slow { get; set; }

    // 5000 rows instead of 95, for the virtual list
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

    public IReadOnlyList<DemoRow> Items
    {
        get
        {
            return _page;
        }
    }

    public long Total
    {
        get
        {
            return _total;
        }
    }

    public int PageSize
    {
        get
        {
            return Size;
        }
    }

    public int CurrentPage
    {
        get
        {
            return _currentPage;
        }
    }

    public bool IsBusy { get; private set; }

    public Exception? LoadError { get; private set; }

    public string? SearchText { get; private set; }

    public bool CanSearch
    {
        get
        {
            return true;
        }
    }

    public DemoRow? Current { get; set; }

    public IReadOnlyCollection<DemoRow> SelectedItems
    {
        get
        {
            return _selected;
        }
    }

    public Task SearchAsync(string? text)
    {
        SearchText = text;
        _currentPage = 0;
        return RefreshAsync();
    }

    public Task GoToPageAsync(int page)
    {
        _currentPage = page;
        return RefreshAsync();
    }

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

    public void SetSearchText(string? text)
    {
        SearchText = text;
        _currentPage = 0;
        _selected.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

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

    public bool IsSelected(DemoRow item)
    {
        return _selected.Contains(item);
    }

    public void ToggleSelected(DemoRow item)
    {
        if (!_selected.Remove(item))
        {
            _selected.Add(item);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        _selected.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<DemoRow> Targets(DemoRow row)
    {
        return _selected.Contains(row) ? [.. _selected] : [row];
    }
}
