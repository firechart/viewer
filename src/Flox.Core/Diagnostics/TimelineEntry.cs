namespace Flox.Core.Diagnostics;

/// <summary>
/// A span (DurationMilliseconds is set) or a mark (it is null) of the startup timeline; times are from its zero.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
/// <param name="ParentId">Identifier of the enclosing span, or null at the top level.</param>
/// <param name="Name">What happened.</param>
/// <param name="AtMilliseconds">When it started, in milliseconds from the zero of the timeline.</param>
/// <param name="DurationMilliseconds">How long it took, or null for a mark.</param>
/// <param name="Depth">Nesting level, zero at the top.</param>
public sealed record TimelineEntry
(
    long Id,
    long? ParentId,
    string Name,
    double AtMilliseconds,
    double? DurationMilliseconds,
    int Depth
);
