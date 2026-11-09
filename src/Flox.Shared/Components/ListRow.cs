namespace Flox.Shared.Components;

// A row as the template of a DataList sees it: the item and how it takes part in the selection
public interface IListRow
{
    bool Selectable { get; }

    bool Selected { get; }

    // Some rows are selected: a tap on a row toggles it
    bool SelectionActive { get; }

    // The list opens the row on a tap (OnRowClick): the tile shows it as something to click
    bool Clickable { get; }

    Task SetSelectedAsync(bool selected);
}

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
    public TItem Item { get; } = item;

    public bool Selectable { get; } = selectable;

    public bool Selected { get; } = selected;

    public bool SelectionActive { get; } = selectionActive;

    public bool Clickable { get; } = clickable;

    public Task SetSelectedAsync(bool selected)
    {
        return setSelected(selected);
    }
}
