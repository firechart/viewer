using Flox.Core.Data;

namespace Flox.Core.Services;

public interface IUserBookmarkService<TKey, TEntity> where TKey : notnull where TEntity : class
{
    Task AddBookmarkAsync(TEntity value);
    Task RemoveBookmarkAsync(TEntity value);
    bool IsBookmarked(TEntity value);

    // Learns which rows of the page the user has bookmarked: one request about the page, not about all bookmarks
    Task LoadBookmarksAsync(IReadOnlyCollection<TEntity> page);
}
