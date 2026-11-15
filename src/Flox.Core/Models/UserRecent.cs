using Flox.Core.Data;

namespace Flox.Core.Models;

/// <summary>A page the user has visited: one row per user and path, its time moved on by every visit.</summary>
public class UserRecent : GuidEntity, IEntityDisplayInfo
{
    /// <summary>The kind of the page.</summary>
    [Index]
    public string Category { get; set; } = default!;

    /// <summary>The text shown for the page.</summary>
    [Index]
    public string DisplayName { get; set; } = default!;

    /// <summary>The address of the page in the application.</summary>
    [Index]
    public string Url { get; set; } = default!;

    /// <summary>The kind of the entity the page shows.</summary>
    [Index]
    public string Type { get; set; } = default!;

    /// <summary>The identity of the entity the page shows.</summary>
    [Index]
    public string EntityId { get; set; } = default!;

    /// <summary>The icon of the page.</summary>
    public string? Icon { get; set; }

    /// <summary>When the page was last visited.</summary>
    [Index]
    public DateTimeOffset VisitedAt { get; set; }
}
