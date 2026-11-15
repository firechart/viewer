namespace Flox.Shared.Components;

/// <summary>
/// The selected rows of a list, kept by key so that the selection survives a reload, and the row the user acted on last.
/// </summary>
public sealed class SelectionModel<TKey, TEntity> where TKey : notnull where TEntity : class
{
    private readonly Func<TEntity, TKey> _keyOf;
    private readonly Dictionary<TKey, TEntity> _selected = [];

    /// <summary>
    /// Creates a selection that identifies rows by the key the function returns.
    /// </summary>
    public SelectionModel(Func<TEntity, TKey> keyOf)
    {
        _keyOf = keyOf;
    }

    /// <summary>
    /// Raised when the selection or the current row changed.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The row the user acted on last (a tap, the context menu); not the same as the selected rows.
    /// </summary>
    public TEntity? Current { get; private set; }

    /// <summary>
    /// The number of selected rows.
    /// </summary>
    public int Count
    {
        get
        {
            return _selected.Count;
        }
    }

    /// <summary>
    /// Whether any row is selected.
    /// </summary>
    public bool Any
    {
        get
        {
            return _selected.Count > 0;
        }
    }

    /// <summary>
    /// The selected rows.
    /// </summary>
    public IReadOnlyCollection<TEntity> Items
    {
        get
        {
            return _selected.Values;
        }
    }

    /// <summary>
    /// Tells whether the row is selected.
    /// </summary>
    public bool IsSelected(TEntity item)
    {
        return _selected.ContainsKey(_keyOf(item));
    }

    /// <summary>
    /// Makes the row the current one.
    /// </summary>
    public void SetCurrent(TEntity? item)
    {
        if (!ReferenceEquals(Current, item))
        {
            Current = item;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Selects the row.
    /// </summary>
    public void Select(TEntity item)
    {
        if (_selected.TryGetValue(_keyOf(item), out var existing) && ReferenceEquals(existing, item))
        {
            return;
        }

        _selected[_keyOf(item)] = item;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deselects the row.
    /// </summary>
    public void Deselect(TEntity item)
    {
        if (_selected.Remove(_keyOf(item)))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Selects the row if it is not selected, otherwise deselects it.
    /// </summary>
    public void Toggle(TEntity item)
    {
        if (IsSelected(item))
        {
            Deselect(item);
        }
        else
        {
            Select(item);
        }
    }

    /// <summary>
    /// Replaces the selected rows with the freshly loaded ones of the same key, so a command acts on current data.
    /// </summary>
    public void Refresh(IEnumerable<TEntity> loaded)
    {
        var changed = false;
        foreach (var item in loaded)
        {
            if (_selected.TryGetValue(_keyOf(item), out var selected) && !ReferenceEquals(selected, item))
            {
                _selected[_keyOf(item)] = item;
                changed = true;
            }

            if (Current != null && !ReferenceEquals(Current, item) && EqualityComparer<TKey>.Default.Equals(_keyOf(Current), _keyOf(item)))
            {
                Current = item;
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The rows a command invoked on a row acts on: the whole selection when the row belongs to it, otherwise the row alone.
    /// </summary>
    public IReadOnlyList<TEntity> Targets(TEntity row)
    {
        return IsSelected(row) ? [.. _selected.Values] : [row];
    }

    /// <summary>
    /// Deselects all rows and clears the current row.
    /// </summary>
    public void Clear()
    {
        if (_selected.Count == 0 && Current == null)
        {
            return;
        }

        _selected.Clear();
        Current = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
