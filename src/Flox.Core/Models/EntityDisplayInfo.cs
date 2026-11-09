namespace Flox.Core.Models;

public class EntityDisplayInfo : IEntityDisplayInfo
{
    public string Category { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string Url { get; set; } = default!;

    // The identity of the entity that outlives its Uid (data rebuilt from a source gets new Uids); the Uid when empty
    public string? EntityId { get; set; }
}
