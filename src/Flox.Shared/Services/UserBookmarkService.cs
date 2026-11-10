using System.Globalization;
using Flox.Core.Models;

namespace Flox.Shared.Services;

[FloxService(typeof(IUserBookmarkService<,>))]
public sealed class UserBookmarkService<TKey, TEntity> : IUserBookmarkService<TKey, TEntity> where TKey : notnull where TEntity : class
{
    private readonly IGuidDataSource<UserBookmark> _bookmarksDataSource;
    private readonly IEnumerable<IEntityFormatterService> _formatters;
    private readonly IServiceProvider _serviceProvider;
    private IDataSource<TKey, TEntity>? _keys;

    public UserBookmarkService(IGuidDataSource<UserBookmark> bookmarksDataSource, IEnumerable<IEntityFormatterService> formatters, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _bookmarksDataSource = bookmarksDataSource;
        _formatters = formatters;
    }

    private readonly HashSet<string> _bookmarked = [];

    // The key of an entity comes from the data source of its type, which knows how to read it
    private TKey KeyOf(TEntity value)
    {
        _keys ??= _serviceProvider.GetRequiredService<IDataSource<TKey, TEntity>>();
        return _keys.GetKey(value);
    }

    private static string KeyToString(TKey key)
    {
        return Convert.ToString(key, CultureInfo.InvariantCulture)!;
    }

    private (EntityDisplayInfo Info, string EntityId)? Describe(TEntity value)
    {
        foreach (var formatter in _formatters)
        {
            if (formatter.Format<TKey, TEntity>(value) is { } info)
            {
                return (info, info.EntityId ?? KeyToString(KeyOf(value)));
            }
        }

        return null;
    }

    private string IdentityOf(TEntity value)
    {
        return Describe(value)?.EntityId ?? KeyToString(KeyOf(value));
    }

    public async Task AddBookmarkAsync(TEntity value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var type = value.GetType();
        if (Describe(value) is not var (info, entityId))
        {
            throw new InvalidOperationException($"Bookmark type {type.Name} is not supported.");
        }

        if (_bookmarked.Contains(entityId))
        {
            return;
        }

        var bookmark = new UserBookmark
        {
            Type = type.FullName!,
            EntityId = entityId,
            Category = info.Category,
            DisplayName = info.DisplayName,
            Url = info.Url
        };
        bookmark.Uid = UserBookmark.KeyOf(bookmark.Type, null, bookmark.EntityId);

        // The same bookmark made in another tab is the same document: that it exists already is what was asked for
        try
        {
            await _bookmarksDataSource.Storage.InsertAsync(bookmark);
        }
        catch (DuplicateException)
        {
        }

        _bookmarked.Add(entityId);
    }

    public bool IsBookmarked(TEntity value)
    {
        return _bookmarked.Contains(IdentityOf(value));
    }

    public async Task LoadBookmarksAsync(IReadOnlyCollection<TEntity> page)
    {
        _bookmarked.Clear();
        if (page.Count == 0)
        {
            return;
        }

        // A request of its own: the data source of the service may be busy with an add or a remove
        var request = new PagedRequest<UserBookmark>
        {
            PageSize = page.Count * 2,
            ReturnTotal = false
        };
        request.Filters.SetValue(nameof(UserBookmark.Type), typeof(TEntity).FullName!);
        request.Filters.SetValue(nameof(UserBookmark.EntityId), page.Select(IdentityOf).ToList(), FilterOperator.In);
        var found = await _bookmarksDataSource.Storage.FindAsync(request);
        foreach (var row in found.Items)
        {
            _bookmarked.Add(row.EntityId);
        }
    }

    public async Task RemoveBookmarkAsync(TEntity value)
    {
        var typeName = value.GetType()
                            .FullName!;
        var entityId = IdentityOf(value);

        await _bookmarksDataSource.Storage.DeleteManyAsync(row => row.Type == typeName && row.EntityId == entityId);
        _bookmarked.Remove(entityId);
    }
}
