namespace FireChart.Viewer.Core.Search;

/// <summary>
/// What the user entered into one field of a form; the same object restores the form when it is opened again. Which properties count depends on the
/// control of the field (<see cref="Forms.SearchFieldControl"/>).
/// </summary>
/// <example>
/// <code>
/// var value = SearchFormEvaluator.NewValue(field);
/// value.Text = "Smith";
/// var conditions = SearchFormEvaluator.Evaluate(field, value);
/// </code>
/// </example>
public sealed class SearchFieldValue
{
    /// <summary>The text of a text, autocomplete or reference field.</summary>
    public string? Text { get; set; }

    /// <summary>The number, or the start of a range of numbers.</summary>
    public decimal? Number { get; set; }

    /// <summary>The end of a range of numbers.</summary>
    public decimal? NumberTo { get; set; }

    /// <summary>The date, or the start of a range of dates.</summary>
    public DateTime? Date { get; set; }

    /// <summary>The end of a range of dates.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>The codes chosen in a list (<see cref="SearchOption.Key"/>).</summary>
    public List<string> Selected { get; } = [];

    /// <summary>The comparator chosen (<c>ge</c>, <c>lt</c>), or <see langword="null"/> for equality.</summary>
    public string? Comparator { get; set; }

    /// <summary>The modifier chosen (<c>exact</c>), or <see langword="null"/>.</summary>
    public string? Modifier { get; set; }

    /// <summary>The unit of a quantity.</summary>
    public string? Unit { get; set; }

    /// <summary>The code system of a token or of the unit of a quantity.</summary>
    public string? System { get; set; }

    /// <summary>
    /// A copy that does not share the list of the codes chosen.
    /// </summary>
    /// <returns>The copy.</returns>
    public SearchFieldValue Clone()
    {
        var result = new SearchFieldValue
        {
            Text = Text,
            Number = Number,
            NumberTo = NumberTo,
            Date = Date,
            DateTo = DateTo,
            Comparator = Comparator,
            Modifier = Modifier,
            Unit = Unit,
            System = System
        };
        result.Selected.AddRange(Selected);
        return result;
    }
}
