using Flox.Core.Data;

namespace Flox.Core.Services;

/// <summary>Keeps the bookmarks of the user for entities of one type.</summary>
/// <typeparam name="TKey">The type of the key of the entity.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public interface IUserBookmarkService<TKey, TEntity> where TKey : notnull where TEntity : class
{
    /// <summary>Bookmarks an entity.</summary>
    /// <param name="value">The entity.</param>
    Task AddBookmarkAsync(TEntity value);
    /// <summary>Removes the bookmark of an entity.</summary>
    /// <param name="value">The entity.</param>
    Task RemoveBookmarkAsync(TEntity value);
    /// <summary>Tells whether an entity is bookmarked, as far as the loaded bookmarks know.</summary>
    /// <param name="value">The entity.</param>
    /// <returns>True when it is bookmarked.</returns>
    bool IsBookmarked(TEntity value);

    /// <summary>Learns which rows of the page the user has bookmarked: one request about the page, not about all bookmarks.</summary>
    /// <param name="page">The rows of the page.</param>
    Task LoadBookmarksAsync(IReadOnlyCollection<TEntity> page);
}
