namespace FireChart.Viewer.Tests.Rendering;

// The clock of a test: a fixed moment and the zone the dates are shown in (the machine's own zone must not matter)
internal sealed class FixedClock(TimeZoneInfo zone, DateTimeOffset? now = null) : TimeProvider
{
    public static FixedClock Offset(int hours, DateTimeOffset? now = null)
    {
        return new FixedClock(TimeZoneInfo.CreateCustomTimeZone($"UTC{hours:+0;-0}", TimeSpan.FromHours(hours), "test", "test"), now);
    }

    public override TimeZoneInfo LocalTimeZone => zone;

    public override DateTimeOffset GetUtcNow()
    {
        return now ?? new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
