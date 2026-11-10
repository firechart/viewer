namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// How a value is shown where a form asks for it: a section of the clinical summary for the values of its records, later a column of a table.
/// </summary>
/// <remarks>
/// Without it a value keeps the digits of the record (<c>1.30</c> stays <c>1.30</c>: trailing zeros are significant figures). The templates get it as
/// <c>fhir_options.decimals</c> and pass it on: <c>{% apply self.value mode: "text", decimals: fhir_options.decimals %}</c>.
/// </remarks>
/// <example>
/// <code>
/// var results = new ClinicalSummaryFormSection { Code = "results", Format = new ValueFormat { Decimals = 2 } };
/// </code>
/// </example>
public sealed class ValueFormat
{
    /// <summary>The most decimal places shown (0 to 10): more are rounded half away from zero, fewer are never padded with zeros.</summary>
    public const int MaxDecimals = 10;

    /// <summary>
    /// The most decimal places of a number (0 to <see cref="MaxDecimals"/>); <see langword="null"/> keeps the digits of the record.
    /// </summary>
    public int? Decimals { get; set; }
}
