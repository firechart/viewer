namespace Flox.Core.Models;

public class BackgroundTaskCleanupRequest
{
    public bool CleanupCompleted { get; set; } = true;

    public bool CleanupErrorneous { get; set; } = true;

    public bool CleanupCancelled { get; set; } = true;

    public DateTimeOffset? OlderThan { get; set; } = null;
}
