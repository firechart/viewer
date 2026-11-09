namespace Flox.Shared.Components;

public enum ListMode
{
    // Pages with a pager under the list
    Paged,

    // Rows are loaded as the list is scrolled
    Virtual
}

public enum ListLayout
{
    // The list takes the height left in its parent, which must be a flex column of a known height; the list scrolls
    Fill,

    // The list is as high as its rows and the page scrolls
    Content
}

// What the commands of a row menu need: the row it was opened on, the rows they act on, and a way to close it
public sealed class RowMenuContext<TItem>
(
    TItem row,
    IReadOnlyList<TItem> targets,
    Action close
) where TItem : class
{
    public TItem Row { get; } = row;

    // The selected rows when the row is one of them, otherwise the row alone
    public IReadOnlyList<TItem> Targets { get; } = targets;

    public void Close()
    {
        close();
    }
}
