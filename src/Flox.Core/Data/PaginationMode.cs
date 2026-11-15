namespace Flox.Core.Data;

/// <summary>How a data source moves between pages.</summary>
public enum PaginationMode
{
    /// <summary>By page number.</summary>
    PageBased,
    /// <summary>By the links of the previous answer.</summary>
    LinkBased
}
