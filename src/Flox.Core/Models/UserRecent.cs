using Flox.Core.Data;

namespace Flox.Core.Models;

// A page the user has visited: one row per user and path, its time moved on by every visit
public class UserRecent : GuidEntity, IEntityDisplayInfo
{
    [Index]
    public string Category { get; set; } = default!;

    [Index]
    public string DisplayName { get; set; } = default!;

    [Index]
    public string Url { get; set; } = default!;

    [Index]
    public string Type { get; set; } = default!;

    [Index]
    public string EntityId { get; set; } = default!;

    public string? Icon { get; set; }

    [Index]
    public DateTimeOffset VisitedAt { get; set; }
}
