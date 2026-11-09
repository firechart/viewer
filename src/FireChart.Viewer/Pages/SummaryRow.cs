namespace FireChart.Viewer.Pages;

/// <summary>
/// The kind of a line of the clinical summary.
/// </summary>
public enum SummaryRowKind
{
    /// <summary>The header of a block.</summary>
    Header,

    /// <summary>A record as its tile.</summary>
    Card,

    /// <summary>The block is loading.</summary>
    Spinner,

    /// <summary>A search of the block failed: a retry.</summary>
    Error,

    /// <summary>The block has more records: the next pages.</summary>
    More
}

/// <summary>
/// One line of the flat list of the clinical summary: the blocks are laid one after another, each as a header, its cards and a footer line.
/// </summary>
/// <param name="Kind">What the line is.</param>
/// <param name="Section">The block.</param>
/// <param name="Item">The record of a card.</param>
/// <param name="Tile">Its tile.</param>
public sealed record SummaryRow(SummaryRowKind Kind, ClinicalSummarySection Section, ResourceSummary? Item = null, TileModel? Tile = null);
