using Flox.Core.Data;

namespace Flox.Core.Models;

public class UserBookmark : GuidEntity, IEntityDisplayInfo
{
    [Index]
    public string Category { get; set; } = default!;

    [Index(Like = true, FullText = FullTextWeight.A)]
    public string DisplayName { get; set; } = default!;

    [Index]
    public string Url { get; set; } = default!;

    [Index]
    public string Type { get; set; } = default!;

    [Index]
    public string EntityId { get; set; } = default!;
}
