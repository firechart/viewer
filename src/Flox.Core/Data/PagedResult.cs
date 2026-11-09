namespace Flox.Core.Data;

public class PagedResult
{
    public long? Total { get; set; }
    public string? FirstLink { get; set; }
    public string? PrevLink { get; set; }
    public string? NextLink { get; set; }
    public string? LastLink { get; set; }
    public string? SelfLink { get; set; }
}

public class PagedResult<T> : PagedResult
{
    public List<T> Items { get; } = [];
}
