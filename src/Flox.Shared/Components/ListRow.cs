namespace Flox.Shared.Components;

/// <summary>
/// A row as the template of a list sees it: the item and how it takes part in the selection.
/// </summary>
public interface IListRow
{
    /// <summary>
    /// Whether the row can be selected.
    /// </summary>
    bool Selectable { get; }

    /// <summary>
    /// Whether the row is selected.
    /// </summary>
    bool Selected { get; }

    /// <summary>
    /// Some rows are selected: a tap on a row toggles it.
    /// </summary>
    bool SelectionActive { get; }

    /// <summary>
    /// The list opens the row on a tap: the tile shows it as something to click.
    /// </summary>
    bool Clickable { get; }

    /// <summary>
    /// Selects or deselects the row.
    /// </summary>
    Task SetSelectedAsync(bool selected);
}

/// <summary>
/// A row as the template of a list sees it, for an item of the list.
/// </summary>
/// <param name="item">The item of the row.</param>
/// <param name="selectable">Whether the row can be selected.</param>
/// <param name="selected">Whether the row is selected.</param>
/// <param name="selectionActive">Whether some rows are selected.</param>
/// <param name="clickable">Whether the list opens the row on a tap.</param>
/// <param name="setSelected">Selects or deselects the row.</param>
public sealed class ListRow<TItem>
(
    TItem item,
    bool selectable,
    bool selected,
    bool selectionActive,
    bool clickable,
    Func<bool, Task> setSelected
) : IListRow where TItem : class
{
    /// <summary>
    /// The item of the row.
    /// </summary>
    public TItem Item { get; } = item;

    /// <inheritdoc />
    public bool Selectable { get; } = selectable;

    /// <inheritdoc />
    public bool Selected { get; } = selected;

    /// <inheritdoc />
    public bool SelectionActive { get; } = selectionActive;

    /// <inheritdoc />
    public bool Clickable { get; } = clickable;

    /// <inheritdoc />
    public Task SetSelectedAsync(bool selected)
    {
        return setSelected(selected);
    }
}
