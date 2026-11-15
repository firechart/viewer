namespace Flox.Core.Diagnostics;

/// <summary>
/// Sends the startup timeline somewhere as a trace; implemented only where there is such a place and the user allows it.
/// </summary>
public interface IStartupTraceExporter
{
    /// <summary>
    /// Exports the entries of the timeline.
    /// </summary>
    /// <param name="entries">The entries of the startup timeline.</param>
    /// <param name="attributes">Attributes to attach to the trace.</param>
    Task ExportAsync(IReadOnlyList<TimelineEntry> entries, IReadOnlyDictionary<string, string> attributes);
}
