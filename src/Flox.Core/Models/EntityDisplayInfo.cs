namespace Flox.Core.Models;

/// <summary>The default <see cref="IEntityDisplayInfo"/>.</summary>
public class EntityDisplayInfo : IEntityDisplayInfo
{
    /// <summary>The kind of the entity.</summary>
    public string Category { get; set; } = default!;

    /// <summary>The text shown for the entity.</summary>
    public string DisplayName { get; set; } = default!;

    /// <summary>The address of the entity in the application.</summary>
    public string Url { get; set; } = default!;

    /// <summary>The identity of the entity that outlives its Uid (data rebuilt from a source gets new Uids); the Uid when empty.</summary>
    public string? EntityId { get; set; }
}
