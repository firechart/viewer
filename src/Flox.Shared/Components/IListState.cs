namespace Flox.Shared.Components;

/// <summary>
/// A slice of the rows of a list with the number of rows of the whole list.
/// </summary>
/// <param name="Items">The rows of the slice.</param>
/// <param name="Total">The number of rows of the whole list.</param>
public readonly record struct ListRange<TItem>(IReadOnlyList<TItem> Items, long Total);

/// <summary>
/// What a list needs from the state behind it: the rows, the paging, the search, the selection and the commands. A data source state and an array both give it, so one list shows either.
/// </summary>
public interface IListState<TItem> where TItem : class
{
    /// <summary>
    /// Raised when the rows, the selection or another property changed.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// The rows currently shown.
    /// </summary>
    IReadOnlyList<TItem> Items { get; }

    /// <summary>
    /// The number of rows of all pages; the number of <see cref="Items"/> when the list is not paged.
    /// </summary>
    long Total { get; }

    /// <summary>
    /// The number of rows on a page; zero when the list is not paged.
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// The zero-based index of the current page.
    /// </summary>
    int CurrentPage { get; }

    /// <summary>
    /// Whether rows are being loaded.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// The error of the last load, or null; <see cref="Items"/> are then the rows of the last good load.
    /// </summary>
    Exception? LoadError { get; }

    /// <summary>
    /// The text of the current search, if any.
    /// </summary>
    string? SearchText { get; }

    /// <summary>
    /// Whether the list can be searched.
    /// </summary>
    bool CanSearch { get; }

    /// <summary>
    /// Searches the rows for the text and loads the result.
    /// </summary>
    Task SearchAsync(string? text);

    /// <summary>
    /// Sets the search text without loading: a virtual list loads on its own.
    /// </summary>
    void SetSearchText(string? text);

    /// <summary>
    /// Virtual mode: loads the rows from <c>skip</c> on without touching the paging or the rows of the state.
    /// </summary>
    Task<ListRange<TItem>> LoadRangeAsync(int skip, int count, CancellationToken cancellationToken);

    /// <summary>
    /// Tells whether two rows are the same row, so that a virtual list sees a range loaded again as the same rows and not as rows
    /// inserted above. By reference by default: a state whose rows stay the same instances; a state that loads new instances compares
    /// by the key of the row.
    /// </summary>
    IEqualityComparer<TItem> ItemComparer
    {
        get
        {
            return ReferenceEqualityComparer.Instance;
        }
    }

    /// <summary>
    /// Loads the page with the given zero-based index.
    /// </summary>
    Task GoToPageAsync(int page);

    /// <summary>
    /// Loads the current request again.
    /// </summary>
    Task RefreshAsync();

    /// <summary>
    /// The row the user acted on last (a tap, the context menu).
    /// </summary>
    TItem? Current { get; set; }

    /// <summary>
    /// The rows that are selected.
    /// </summary>
    IReadOnlyCollection<TItem> SelectedItems { get; }

    /// <summary>
    /// Tells whether the row is selected.
    /// </summary>
    bool IsSelected(TItem item);

    /// <summary>
    /// Selects the row if it is not selected, otherwise deselects it.
    /// </summary>
    void ToggleSelected(TItem item);

    /// <summary>
    /// Deselects all rows.
    /// </summary>
    void ClearSelection();

    /// <summary>
    /// The rows a command invoked on a row acts on: the selection when the row belongs to it, the row alone otherwise.
    /// </summary>
    IReadOnlyList<TItem> Targets(TItem row);
}
