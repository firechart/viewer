namespace Flox.Shared.Components;

// What a DataList needs from the state behind it: the rows, the paging, the search, the selection and the commands. A data source
// state and an array both give it, so one list shows either
public readonly record struct ListRange<TItem>(IReadOnlyList<TItem> Items, long Total);

public interface IListState<TItem> where TItem : class
{
    event EventHandler? Changed;

    IReadOnlyList<TItem> Items { get; }

    // The rows of all pages; the number of Items when the list is not paged
    long Total { get; }

    // Zero when the list is not paged
    int PageSize { get; }

    int CurrentPage { get; }

    bool IsBusy { get; }

    // The last load failed; Items are then the rows of the last good one
    Exception? LoadError { get; }

    string? SearchText { get; }

    bool CanSearch { get; }

    Task SearchAsync(string? text);

    // Sets the search text without loading: a virtual list loads on its own
    void SetSearchText(string? text);

    // Virtual mode: the rows from skip on, loaded without touching the paging or the rows of the state
    Task<ListRange<TItem>> LoadRangeAsync(int skip, int count, CancellationToken cancellationToken);

    Task GoToPageAsync(int page);

    Task RefreshAsync();

    // The row the user acted on last (a tap, the context menu)
    TItem? Current { get; set; }

    IReadOnlyCollection<TItem> SelectedItems { get; }

    bool IsSelected(TItem item);

    void ToggleSelected(TItem item);

    void ClearSelection();

    // What a command invoked on a row acts on: the selection when the row belongs to it, the row alone otherwise
    IReadOnlyList<TItem> Targets(TItem row);
}
