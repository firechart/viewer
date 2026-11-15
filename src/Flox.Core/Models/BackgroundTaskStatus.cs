namespace Flox.Core.Models;

/// <summary>Where a background task is in its life cycle.</summary>
public enum BackgroundTaskStatus
{
    /// <summary>Queued and not started.</summary>
    Pending,

    /// <summary>Running.</summary>
    Running,

    /// <summary>Finished successfully.</summary>
    Completed,

    /// <summary>Finished with an error.</summary>
    Error,

    /// <summary>A cancel was requested and the task is stopping.</summary>
    Cancelling,

    /// <summary>Stopped by a cancel.</summary>
    Cancelled,

    /// <summary>Stopped because it ran longer than its timeout.</summary>
    Timeout
}
