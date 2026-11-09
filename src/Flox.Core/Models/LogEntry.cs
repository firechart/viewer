using Flox.Core.Util;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Models;

public class LogEntry
{
    public Guid Id { get; set; } = GuidUtil.CreateUuid();
    public DateTimeOffset DateTime { get; set; } = DateTimeOffset.UtcNow;
    public LogLevel LogLevel { get; set; } = LogLevel.None;
    public string Text { get; set; } = null!;
}
