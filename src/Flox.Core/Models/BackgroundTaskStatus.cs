namespace Flox.Core.Models;

public enum BackgroundTaskStatus
{
    Pending,

    Running,

    Completed,

    Error,

    Cancelling,

    Cancelled,

    Timeout
}
