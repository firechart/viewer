using Flox.Core.Data;
using Flox.Core.Models;

namespace Flox.Core.Services;

/// <summary>Describes an entity as a link for lists such as bookmarks and recent pages.</summary>
public interface IEntityFormatterService
{
    /// <summary>Describes an entity for display.</summary>
    /// <typeparam name="TKey">The type of the key of the entity.</typeparam>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <param name="value">The entity.</param>
    /// <returns>The description, or null when the entity cannot be shown.</returns>
    EntityDisplayInfo? Format<TKey, TEntity>(TEntity value) where TKey : notnull;
}
