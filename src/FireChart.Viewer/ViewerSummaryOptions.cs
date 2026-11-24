namespace FireChart.Viewer;

/// <summary>
/// How a summary (and a report of components) loads its records: the size of a page of its searches, and whether it is shown only once everything
/// has come (option <c>summary</c> of the configuration).
/// </summary>
/// <remarks>
/// A summary loads its blocks in parallel and draws each as it comes, so the blocks below move while those above fill. With <see cref="LoadAll"/> the
/// page is drawn hidden behind a panel of progress (what has come, Cancel) and appears whole; Cancel shows what has come, marked incomplete. A larger
/// <see cref="PageSize"/> asks the server fewer times; a page size of the summary form wins over it.
/// </remarks>
/// <example>
/// <code>
/// &lt;FhirViewer Server=@server Summary=@(new ViewerSummaryOptions { PageSize = 100, LoadAll = true })/&gt;
/// </code>
/// </example>
public sealed record ViewerSummaryOptions
{
    /// <summary>The smallest page size accepted.</summary>
    public const int MinPageSize = 1;

    /// <summary>The largest page size accepted (servers cap <c>_count</c> on their own anyway).</summary>
    public const int MaxPageSize = 500;

    /// <summary>The options when the host gives none: pages of the form, else of 20, drawn as they come.</summary>
    public static readonly ViewerSummaryOptions Default = new();

    /// <summary>
    /// The size of a page of a search (<c>_count</c>) when the summary form does not give one; <see langword="null"/> is the default of the Viewer
    /// (20). Out of <see cref="MinPageSize"/>..<see cref="MaxPageSize"/> it is clamped.
    /// </summary>
    public int? PageSize { get; init; }

    /// <summary>Every record of every block is loaded before the page is shown, behind a panel of progress with Cancel.</summary>
    public bool LoadAll { get; init; }

    /// <summary>The page size within the accepted range, or <see langword="null"/>.</summary>
    /// <returns>The page size to use.</returns>
    public int? EffectivePageSize()
    {
        return PageSize is { } size ? Math.Clamp(size, MinPageSize, MaxPageSize) : null;
    }
}
