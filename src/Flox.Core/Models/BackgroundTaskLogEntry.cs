using Flox.Core.Data;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Models;

/// <summary>A line of the log of one background task.</summary>
public class BackgroundTaskLogEntry : GuidEntity
{
    /// <summary>The text of the entry.</summary>
    public string? Message { get; set; }
    /// <summary>When the entry was written.</summary>
    public DateTimeOffset DateTime { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>The level of the entry.</summary>
    public LogLevel LogLevel { get; set; }
    /// <summary>The task the entry belongs to.</summary>
    public Guid BackgroundTaskId { get; set; }
}
