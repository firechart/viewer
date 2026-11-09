namespace Flox.Shared.Components;

// The rows of a list given as an array: not paged, not searched, selected by reference
public sealed class ArrayListState<TItem> : IListState<TItem> where TItem : class
{
    private readonly HashSet<TItem> _selected = new HashSet<TItem>(ReferenceEqualityComparer.Instance);

    public event EventHandler? Changed;

    public IReadOnlyList<TItem> Items { get; private set; } = [];

    public long Total
    {
        get
        {
            return Items.Count;
        }
    }

    public int PageSize
    {
        get
        {
            return 0;
        }
    }

    public int CurrentPage
    {
        get
        {
            return 0;
        }
    }

    public bool IsBusy
    {
        get
        {
            return false;
        }
    }

    public Exception? LoadError
    {
        get
        {
            return null;
        }
    }

    public string? SearchText
    {
        get
        {
            return null;
        }
    }

    public bool CanSearch
    {
        get
        {
            return false;
        }
    }

    public TItem? Current { get; set; }

    public IReadOnlyCollection<TItem> SelectedItems
    {
        get
        {
            return _selected;
        }
    }

    public void SetItems(IEnumerable<TItem>? items)
    {
        Items = items is null ? [] : [.. items];
        _selected.IntersectWith(Items);
        if (Current != null && !_selected.Contains(Current) && !Items.Contains(Current))
        {
            Current = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task SearchAsync(string? text)
    {
        return Task.CompletedTask;
    }

    public void SetSearchText(string? text)
    {
    }

    public Task<ListRange<TItem>> LoadRangeAsync(int skip, int count, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ListRange<TItem>([.. Items.Skip(skip).Take(count)], Items.Count));
    }

    public Task GoToPageAsync(int page)
    {
        return Task.CompletedTask;
    }

    public Task RefreshAsync()
    {
        return Task.CompletedTask;
    }

    public bool IsSelected(TItem item)
    {
        return _selected.Contains(item);
    }

    public void ToggleSelected(TItem item)
    {
        if (!_selected.Remove(item))
        {
            _selected.Add(item);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        if (_selected.Count > 0)
        {
            _selected.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<TItem> Targets(TItem row)
    {
        return _selected.Contains(row) ? [.. _selected] : [row];
    }
}
