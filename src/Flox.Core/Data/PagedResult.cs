namespace Flox.Core.Data;

/// <summary>The paging data of an answer: the total and the links to the pages.</summary>
public class PagedResult
{
    /// <summary>The total number of matching entities, when it was asked for.</summary>
    public long? Total { get; set; }
    /// <summary>The link to the first page.</summary>
    public string? FirstLink { get; set; }
    /// <summary>The link to the previous page.</summary>
    public string? PrevLink { get; set; }
    /// <summary>The link to the next page.</summary>
    public string? NextLink { get; set; }
    /// <summary>The link to the last page.</summary>
    public string? LastLink { get; set; }
    /// <summary>The link to this page.</summary>
    public string? SelfLink { get; set; }
}

/// <summary>A page of entities with the paging data.</summary>
public class PagedResult<T> : PagedResult
{
    /// <summary>The entities of the page.</summary>
    public List<T> Items { get; } = [];
}
