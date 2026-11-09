using System.Globalization;

namespace FireChart.Viewer.Core.Search;

/// <summary>
/// How the values of a search are written.
/// </summary>
/// <example>
/// <code>
/// var value = $"{SearchFhirFormat.Number(5.4m)}|{SearchFhirFormat.UcumSystem}|mmol/L";
/// </code>
/// </example>
public static class SearchFhirFormat
{
    /// <summary>The system of the units of UCUM.</summary>
    public const string UcumSystem = "http://unitsofmeasure.org";

    /// <summary>The format of a date without a time.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Escapes the characters with a meaning inside a value of a parameter (<c>\</c>, <c>,</c>, <c>$</c>, <c>|</c>) with a backslash.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The escaped value.</returns>
    /// <example>
    /// <code>
    /// var text = SearchFhirFormat.Escape("a,b"); // a\,b
    /// </code>
    /// </example>
    public static string Escape(string value)
    {
        var result = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is '\\' or ',' or '$' or '|')
            {
                result.Append('\\');
            }

            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    /// A number in the invariant culture.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text (<c>5.4</c>).</returns>
    public static string Number(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A date without a time (<see cref="DateFormat"/>).
    /// </summary>
    /// <param name="value">The date.</param>
    /// <returns>The text (<c>2024-05-17</c>).</returns>
    public static string Date(DateTime value)
    {
        return value.ToString(DateFormat, CultureInfo.InvariantCulture);
    }
}
