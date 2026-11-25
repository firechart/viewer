using System.Globalization;

namespace FireChart.Viewer.Core;

/// <summary>
/// The one rule of the Viewer for a decimal: the digits of the record up to a number of decimal places; more are rounded half away from zero, and
/// the zeros the rounding leaves at the end are dropped with the separator.
/// </summary>
/// <remarks>
/// A record that gives fewer places than the limit keeps them, its trailing zeros included (<c>1.30</c> says the precision of the instrument); a value
/// with more (synthetic data, a computed value) is rounded (<c>3.0212296</c> is <c>3.02</c>, <c>100.004</c> is <c>100</c>). The places come from the
/// FHIR settings of the Viewer (2 by default) or from a <c>Feed</c> of a component that sets <c>decimals</c>.
/// </remarks>
/// <example>
/// <code>
/// DecimalText.Format(3.0212296m, 2, CultureInfo.InvariantCulture); // 3.02
/// DecimalText.Format(100.004m, 2, CultureInfo.InvariantCulture);   // 100
/// DecimalText.Format(1.30m, 2, CultureInfo.InvariantCulture);      // 1.30
/// </code>
/// </example>
public static class DecimalText
{
    /// <summary>The most decimal places a limit may ask for: 10.</summary>
    public const int MaxPlaces = 10;

    /// <summary>
    /// A decimal as the Viewer shows it.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="places">The most decimal places; <see langword="null"/>: the digits of the record. Clamped to 0..<see cref="MaxPlaces"/>.</param>
    /// <param name="culture">The culture of the separator.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// var text = DecimalText.Format(81.51871m, 1, CultureInfo.GetCultureInfo("de-DE")); // 81,5
    /// </code>
    /// </example>
    public static string Format(decimal value, int? places, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (places is not { } limit)
        {
            return value.ToString(culture);
        }

        var digits = Math.Clamp(limit, 0, MaxPlaces);
        return value.Scale <= digits
            ? value.ToString(culture)
            : Math.Round(value, digits, MidpointRounding.AwayFromZero).ToString(digits > 0 ? "0." + new string('#', digits) : "0", culture);
    }

    /// <summary>
    /// A number of a chart (a <see cref="double"/>) by the same rule: at most <paramref name="places"/> decimal places.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="places">The most decimal places; <see langword="null"/>: the number as it is. Clamped to 0..<see cref="MaxPlaces"/>.</param>
    /// <returns>The rounded number.</returns>
    /// <example>
    /// <code>
    /// var y = DecimalText.Round(81.51871, 2); // 81.52
    /// </code>
    /// </example>
    public static double Round(double value, int? places)
    {
        return places is { } limit ? Math.Round(value, Math.Clamp(limit, 0, MaxPlaces), MidpointRounding.AwayFromZero) : value;
    }
}
