namespace Flox.Shared.Components;

/// <summary>
/// How the rows of a list are loaded.
/// </summary>
public enum ListMode
{
    /// <summary>
    /// Pages with a pager under the list.
    /// </summary>
    Paged,

    /// <summary>
    /// Rows are loaded as the list is scrolled.
    /// </summary>
    Virtual
}

/// <summary>
/// How a list takes its height.
/// </summary>
public enum ListLayout
{
    /// <summary>
    /// The list takes the height left in its parent, which must be a flex column of a known height; the list scrolls.
    /// </summary>
    Fill,

    /// <summary>
    /// The list is as high as its rows and the page scrolls.
    /// </summary>
    Content
}

/// <summary>
/// What the commands of a row menu need: the row it was opened on, the rows they act on, and a way to close it.
/// </summary>
/// <param name="row">The row the menu was opened on.</param>
/// <param name="targets">The rows the commands act on.</param>
/// <param name="close">Closes the menu.</param>
public sealed class RowMenuContext<TItem>
(
    TItem row,
    IReadOnlyList<TItem> targets,
    Action close
) where TItem : class
{
    /// <summary>
    /// The row the menu was opened on.
    /// </summary>
    public TItem Row { get; } = row;

    /// <summary>
    /// The selected rows when the row is one of them, otherwise the row alone.
    /// </summary>
    public IReadOnlyList<TItem> Targets { get; } = targets;

    /// <summary>
    /// Closes the menu.
    /// </summary>
    public void Close()
    {
        close();
    }
}
