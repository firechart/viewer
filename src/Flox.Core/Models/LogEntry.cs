using Flox.Core.Util;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Models;

/// <summary>A line of the log of the application.</summary>
public class LogEntry
{
    /// <summary>The id of the entry.</summary>
    public Guid Id { get; set; } = GuidUtil.CreateUuid();
    /// <summary>When the entry was written.</summary>
    public DateTimeOffset DateTime { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>The level of the entry.</summary>
    public LogLevel LogLevel { get; set; } = LogLevel.None;
    /// <summary>The text of the entry.</summary>
    public string Text { get; set; } = null!;
}
