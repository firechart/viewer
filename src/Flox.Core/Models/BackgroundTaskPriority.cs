namespace Flox.Core.Models;

/// <summary>How soon a background task should be taken from the queue.</summary>
public enum BackgroundTaskPriority
{
    /// <summary>The lowest priority.</summary>
    Low = 0,

    /// <summary>The usual priority.</summary>
    Normal = 1,

    /// <summary>Higher than usual.</summary>
    High = 2,

    /// <summary>The highest priority.</summary>
    Critical = 3
}
