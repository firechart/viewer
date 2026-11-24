using System.Globalization;

namespace FireChart.Viewer.Core.Reports;

// How the texts of a report are written: the culture of the reader, the zone of the display (a moment with an offset is shown in it), where the
// words of bare codes come from (a terminology server; none: the code itself), and the most decimal places of a number (the rows of a section that
// says so; none: the digits of the record)
internal sealed class ReportFormat(CultureInfo culture, TimeZoneInfo zone, IReportDisplays? displays = null, TimeProvider? clock = null, ReportStrings? strings = null,
                                   int? decimals = null)
{
    public int? Decimals => decimals;

    // The same format with at most these decimal places
    public ReportFormat WithDecimals(int? places)
    {
        return places == decimals ? this : new ReportFormat(culture, zone, displays, clock, strings, places);
    }

    public CultureInfo Culture => culture;

    public TimeZoneInfo Zone => zone;

    // Today in the zone of the display: the day an age is counted to
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime((clock ?? TimeProvider.System).GetUtcNow(), zone).DateTime);

    // The texts of the components in the language of the reader (locales/strings.*.resx)
    public ReportStrings Strings => strings ?? ReportStrings.Empty;

    // The word of a code that has no display of its own; null when none is known (yet)
    public string? DisplayOf(string system, string code)
    {
        return displays?.DisplayOf(system, code);
    }

    // The culture alone, in the zone of the machine: a title, a test
    public static implicit operator ReportFormat(CultureInfo culture)
    {
        return new ReportFormat(culture, TimeZoneInfo.Local);
    }
}

// The words of codes a record gives without a display (a terminology server); a code it does not know yet it may look up and announce later
internal interface IReportDisplays
{
    string? DisplayOf(string system, string code);
}
