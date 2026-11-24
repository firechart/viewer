namespace FireChart.Viewer.Pages;

/// <summary>
/// The kind of a line of a list of resources.
/// </summary>
public enum ListRowKind
{
    /// <summary>A record as its tile.</summary>
    Card,

    /// <summary>The note that there is no record.</summary>
    Empty,

    /// <summary>The line that asks for the next page.</summary>
    More,

    /// <summary>The note that the list read as many records as it may: the rest is found with the filter.</summary>
    Limit,

    /// <summary>The tile in the place of <see cref="More"/> while the next page is read.</summary>
    Loading
}

/// <summary>
/// One line of a list of resources.
/// </summary>
/// <param name="Kind">What the line is.</param>
/// <param name="Item">The record of a <see cref="ListRowKind.Card"/>.</param>
public sealed record ListRow(ListRowKind Kind, ResourceSummary? Item = null);
