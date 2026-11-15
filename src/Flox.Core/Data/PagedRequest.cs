namespace Flox.Core.Data;

/// <summary>A request for a page of entities: filters, sorts, window, full-text search and the fields to load.</summary>
public class PagedRequest<TEntity>
{
    private int _pageSize = Constants.DEFAULT_PAGE_SIZE;

    private int _skip;

    /// <summary>Tells whether the answer should carry the total number of matching entities.</summary>
    public bool ReturnTotal { get; set; } = true;

    /// <summary>The filters of the request.</summary>
    public FilterCollection<TEntity> Filters { get; } = [];

    /// <summary>
    ///     Has priority over paging parameters. If set, filters and other parameters are ignored
    /// </summary>
    public string? Link { get; set; }

    /// <summary>Extra HTTP headers that a remote storage sends with the request.</summary>
    public Dictionary<string, string> CustomHeaders { get; } = [];

    /// <summary>Extra query parameters that a remote storage sends with the request.</summary>
    public Dictionary<string, string> CustomQueryParameters { get; } = [];

    /// <summary>
    /// A view of <see cref="Skip"/> and <see cref="PageSize"/> for pagers: the zero-based page that holds the first row. Skip is what is
    /// loaded, so a window that does not start at a page boundary (a virtual list) is not rounded to one.
    /// </summary>
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

    /// <summary>The number of rows to load; zero or less is no limit. <see cref="Top"/> is the same number under the name of OData and LINQ.</summary>
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

    /// <summary>The number of rows to skip before the first one that is loaded.</summary>
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

    /// <summary>The sorts of the request.</summary>
    public SortCollection<TEntity> Sorts { get; } = [];

    /// <summary>The names of the fields to load; empty means all of them.</summary>
    public List<string> Fields { get; } = [];

    /// <summary>The number of rows to load, as <see cref="PageSize"/>.</summary>
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
    }
}
