namespace Flox.Core.Data;

public class PagedRequest<TEntity>
{
    private int _pageSize = Constants.DEFAULT_PAGE_SIZE;

    private int _skip;

    public bool ReturnTotal { get; set; } = true;

    public FilterCollection<TEntity> Filters { get; } = [];

    /// <summary>
    ///     Has priority over paging parameters. If set, filters and other parameters are ignored
    /// </summary>
    public string? Link { get; set; }

    public Dictionary<string, string> CustomHeaders { get; } = [];

    public Dictionary<string, string> CustomQueryParameters { get; } = [];

    // A view of Skip and PageSize for pagers: the page that holds the first row. Skip is what is loaded, so a window that
    // does not start at a page boundary (a virtual list) is not rounded to one
    public int Page
    {
        get
        {
            return _pageSize > 0 ? _skip / _pageSize : 0;
        }

        set
        {
            if (_pageSize > 0)
            {
                _skip = value * _pageSize;
            }
        }
    }

    // The number of rows to load; zero or less is no limit. Top is the same number under the name of OData and LINQ
    public int PageSize
    {
        get
        {
            return _pageSize;
        }

        set
        {
            _pageSize = value;
        }
    }

    public int Skip
    {
        get
        {
            return _skip;
        }

        set
        {
            _skip = value;
        }
    }

    public SortCollection<TEntity> Sorts { get; } = [];

    public List<string> Fields { get; } = [];

    public int Top
    {
        get
        {
            return PageSize;
        }
    }

    /// <summary>
    ///     Should be handled by storage if FTS is supported
    /// </summary>
    public string? FullTextSearchString { get; set; }

    /// <summary>
    ///     Should be handled by storage if user bookmarks are supported
    /// </summary>
    public bool BookmarkedOnly { get; set; }

    internal void Reset()
    {
        Filters.Clear();
        Link = null;
        CustomHeaders.Clear();
        CustomQueryParameters.Clear();
        Skip = 0;
        Sorts.Clear();
        Fields.Clear();
        FullTextSearchString = null;
        BookmarkedOnly = false;
    }
}
