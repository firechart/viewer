namespace Flox.Core.Diagnostics;

// A span (DurationMilliseconds is set) or a mark (it is null) of the startup timeline; times are from its zero
public sealed record TimelineEntry
(
    long Id,
    long? ParentId,
    string Name,
    double AtMilliseconds,
    double? DurationMilliseconds,
    int Depth
);
