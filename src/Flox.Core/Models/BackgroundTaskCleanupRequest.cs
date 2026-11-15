namespace Flox.Core.Models;

/// <summary>Which finished background tasks to delete.</summary>
public class BackgroundTaskCleanupRequest
{
    /// <summary>Delete the tasks that completed.</summary>
    public bool CleanupCompleted { get; set; } = true;

    /// <summary>Delete the tasks that ended with an error.</summary>
    public bool CleanupErrorneous { get; set; } = true;

    /// <summary>Delete the tasks that were cancelled.</summary>
    public bool CleanupCancelled { get; set; } = true;

    /// <summary>Delete only the tasks that finished before this moment; all of them when null.</summary>
    public DateTimeOffset? OlderThan { get; set; } = null;
}
