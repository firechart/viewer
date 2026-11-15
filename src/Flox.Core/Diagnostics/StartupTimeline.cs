using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Diagnostics;

/// <summary>
/// Where the time of a start goes: marks and spans on one clock that starts with the first use (so touch it as early as possible), printed as one table and available as a tree of entries (IStartupTraceExporter turns it into a trace). The clock is the process's own: in the browser the time before Main (download, runtime) is not on it, the report of the client says how late the timeline began.
/// </summary>
public static class StartupTimeline
{
    private static readonly long s_origin = Stopwatch.GetTimestamp();
    private static readonly DateTime s_originUtc = DateTime.UtcNow;
    private static readonly Lock s_lock = new Lock();
    private static readonly List<TimelineEntry> s_entries = [];
    private static readonly AsyncLocal<Scope?> s_current = new AsyncLocal<Scope?>();
    private static long s_lastId;

    /// <summary>
    /// Milliseconds since the zero of the timeline.
    /// </summary>
    public static double ElapsedMilliseconds
    {
        get
        {
            return Stopwatch.GetElapsedTime(s_origin)
                 .TotalMilliseconds;
        }
    }

    /// <summary>
    /// The wall-clock time of the zero of the timeline.
    /// </summary>
    public static DateTime OriginUtc
    {
        get
        {
            return s_originUtc;
        }
    }

    /// <summary>
    /// Records a moment: something has happened by now.
    /// </summary>
    public static void Mark(string name)
    {
        var parent = s_current.Value;
        Add(new TimelineEntry(NextId(), parent?.Id, name, ElapsedMilliseconds, null, parent?.Depth + 1 ?? 0));
    }

    /// <summary>
    /// Starts a stretch of work; dispose it when the work is done. Spans nest, the table indents the inner ones.
    /// </summary>
    public static IDisposable Span(string name)
    {
        return new Scope(name);
    }

    /// <summary>
    /// Returns everything recorded so far, in the order of the start.
    /// </summary>
    public static IReadOnlyList<TimelineEntry> Snapshot()
    {
        lock (s_lock)
        {
            return [.. s_entries.OrderBy(e => e.AtMilliseconds)];
        }
    }

    /// <summary>
    /// Writes everything recorded so far to the log as one table; call it again later for a longer table.
    /// </summary>
    public static void Report(ILogger logger, string title)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogInformation("{Title}{NewLine}{Table}", title, Environment.NewLine, Format());
    }

    /// <summary>
    /// Returns everything recorded so far as a text table.
    /// </summary>
    public static string Format()
    {
        var table = new StringBuilder();
        table.AppendLine("  at (ms)   took (ms)  what");
        foreach (var entry in Snapshot())
        {
            table.Append(entry.AtMilliseconds
                              .ToString("F0", CultureInfo.InvariantCulture)
                              .PadLeft(9));
            table.Append("  ");
            table.Append(entry.DurationMilliseconds is { } took
                             ? took.ToString("F0", CultureInfo.InvariantCulture)
                                   .PadLeft(9)
                             : string.Empty.PadLeft(9));
            table.Append("  ");
            table.Append(' ', entry.Depth * 2);
            table.AppendLine(entry.Name);
        }

        return table.ToString();
    }

    private static long NextId()
    {
        return Interlocked.Increment(ref s_lastId);
    }

    private static void Add(TimelineEntry entry)
    {
        lock (s_lock)
        {
            s_entries.Add(entry);
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly string _name;
        private readonly double _startedAt;
        private readonly Scope? _parent;
        private bool _disposed;

        public Scope(string name)
        {
            _name = name;
            _startedAt = ElapsedMilliseconds;
            _parent = s_current.Value;
            Id = NextId();
            Depth = _parent?.Depth + 1 ?? 0;
            s_current.Value = this;
        }

        public long Id { get; }

        public int Depth { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            s_current.Value = _parent;
            Add(new TimelineEntry(Id, _parent?.Id, _name, _startedAt, ElapsedMilliseconds - _startedAt, Depth));
        }
    }
}
