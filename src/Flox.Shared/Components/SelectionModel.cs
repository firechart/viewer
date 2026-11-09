namespace Flox.Shared.Components;

public sealed class SelectionModel<TKey, TEntity> where TKey : notnull where TEntity : class
{
    private readonly Func<TEntity, TKey> _keyOf;
    private readonly Dictionary<TKey, TEntity> _selected = [];

    public SelectionModel(Func<TEntity, TKey> keyOf)
    {
        _keyOf = keyOf;
    }

    public event EventHandler? Changed;

    // The row the user acted on last (a tap, the context menu); not the same as the selected rows
    public TEntity? Current { get; private set; }

    public int Count
    {
        get
        {
            return _selected.Count;
        }
    }

    public bool Any
    {
        get
        {
            return _selected.Count > 0;
        }
    }

    public IReadOnlyCollection<TEntity> Items
    {
        get
        {
            return _selected.Values;
        }
    }

    public bool IsSelected(TEntity item)
    {
        return _selected.ContainsKey(_keyOf(item));
    }

    public void SetCurrent(TEntity? item)
    {
        if (!ReferenceEquals(Current, item))
        {
            Current = item;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Select(TEntity item)
    {
        if (_selected.TryGetValue(_keyOf(item), out var existing) && ReferenceEquals(existing, item))
        {
            return;
        }

        _selected[_keyOf(item)] = item;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Deselect(TEntity item)
    {
        if (_selected.Remove(_keyOf(item)))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

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

    // Replaces the selected rows with the freshly loaded ones of the same key, so a command acts on current data
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

    // What a command invoked on a row acts on: the whole selection when the row belongs to it, otherwise the row alone
    public IReadOnlyList<TEntity> Targets(TEntity row)
    {
        return IsSelected(row) ? [.. _selected.Values] : [row];
    }

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
