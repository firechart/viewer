namespace Flox.Shared.Components;

/// <summary>
/// The rows of a list given as an array: not paged, not searched, selected by reference.
/// </summary>
public sealed class ArrayListState<TItem> : IListState<TItem> where TItem : class
{
    private readonly HashSet<TItem> _selected = new HashSet<TItem>(ReferenceEqualityComparer.Instance);

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IReadOnlyList<TItem> Items { get; private set; } = [];

    /// <inheritdoc />
    public long Total
    {
        get
        {
            return Items.Count;
        }
    }

    /// <inheritdoc />
    public int PageSize
    {
        get
        {
            return 0;
        }
    }

    /// <inheritdoc />
    public int CurrentPage
    {
        get
        {
            return 0;
        }
    }

    /// <inheritdoc />
    public bool IsBusy
    {
        get
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Exception? LoadError
    {
        get
        {
            return null;
        }
    }

    /// <inheritdoc />
    public string? SearchText
    {
        get
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool CanSearch
    {
        get
        {
            return false;
        }
    }

    /// <inheritdoc />
    public TItem? Current { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<TItem> SelectedItems
    {
        get
        {
            return _selected;
        }
    }

    /// <summary>
    /// Replaces the rows, drops from the selection and the current row what is no longer among them, and raises <see cref="Changed"/>.
    /// </summary>
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

    /// <inheritdoc />
    public Task SearchAsync(string? text)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void SetSearchText(string? text)
    {
    }

    /// <inheritdoc />
    public Task<ListRange<TItem>> LoadRangeAsync(int skip, int count, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ListRange<TItem>([.. Items.Skip(skip).Take(count)], Items.Count));
    }

    /// <inheritdoc />
    public Task GoToPageAsync(int page)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RefreshAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool IsSelected(TItem item)
    {
        return _selected.Contains(item);
    }

    /// <inheritdoc />
    public void ToggleSelected(TItem item)
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
        if (_selected.Count > 0)
        {
            _selected.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TItem> Targets(TItem row)
    {
        return _selected.Contains(row) ? [.. _selected] : [row];
    }
}
