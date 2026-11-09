using Flox.Core.Data;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Models;

public class BackgroundTaskLogEntry : GuidEntity
{
    public string? Message { get; set; }
    public DateTimeOffset DateTime { get; set; } = DateTimeOffset.UtcNow;
    public LogLevel LogLevel { get; set; }
    public Guid BackgroundTaskId { get; set; }
}
