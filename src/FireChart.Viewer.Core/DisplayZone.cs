using System.Globalization;

namespace FireChart.Viewer.Core;

/// <summary>
/// The zone the times of the templates are shown in (the zone of the device), for a line on the page.
/// </summary>
public static class DisplayZone
{
    /// <summary>
    /// The zone as a short text.
    /// </summary>
    /// <param name="zone">The zone; the local one by default.</param>
    /// <param name="now">The moment of the offset; now by default.</param>
    /// <returns>The offset and the id of the zone (<c>UTC+01:00 Europe/Berlin</c>).</returns>
    /// <example>
    /// <code>
    /// var line = DisplayZone.Text(); // "UTC+01:00 Europe/Berlin"
    /// </code>
    /// </example>
    public static string Text(TimeZoneInfo? zone = null, DateTimeOffset? now = null)
    {
        zone ??= TimeZoneInfo.Local;
        var offset = zone.GetUtcOffset(now ?? DateTimeOffset.UtcNow);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00} {zone.Id}");
    }
}
