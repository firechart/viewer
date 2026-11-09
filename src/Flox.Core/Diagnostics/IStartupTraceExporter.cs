namespace Flox.Core.Diagnostics;

// Sends the startup timeline somewhere as a trace; implemented only where there is such a place and the user allows it
public interface IStartupTraceExporter
{
    Task ExportAsync(IReadOnlyList<TimelineEntry> entries, IReadOnlyDictionary<string, string> attributes);
}
