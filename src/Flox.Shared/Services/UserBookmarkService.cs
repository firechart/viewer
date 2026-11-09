using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Flox.Core.Models;
using Flox.Resources;
using FluentValidation;

namespace Flox.Shared.Services;

[FloxService(typeof(IUserBookmarkService<,>))]
public sealed class UserBookmarkService<TKey, TEntity> : IUserBookmarkService<TKey, TEntity> where TKey : notnull where TEntity : class
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IGuidDataSource<UserBookmark> _bookmarksDataSource;
    private readonly IEnumerable<IEntityFormatterService> _formatters;
    private readonly IServiceProvider _serviceProvider;
    private IDataSource<TKey, TEntity>? _keys;

    public UserBookmarkService(IGuidDataSource<UserBookmark> bookmarksDataSource, IEnumerable<IEntityFormatterService> formatters, IAuthenticationService authenticationService, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _authenticationService = authenticationService;
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

    // One key for a user and a bookmarked entity: adding twice, on one device or on two, is one document
    private static Guid BookmarkKey(string? userId, string type, string entityId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}|{type}|{entityId}"));
        return new Guid(hash.AsSpan(0, 16));
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

    private static void EnsureSignedIn(ClaimsPrincipal user)
    {
        if (!user.IsAuthenticated())
        {
            throw new UnauthenticatedException();
        }
    }

    public async Task AddBookmarkAsync(TEntity value)
    {
        var user = await _authenticationService.GetCurrentUserAsync();
        EnsureSignedIn(user);

        ArgumentNullException.ThrowIfNull(value);

        var type = value.GetType();
        if (Describe(value) is not var (info, entityId))
        {
            throw new InvalidOperationException($"Bookmark type {type.Name} is not supported.");
        }

        // The same rule as the validator of the server has; here the user hears of it before anything is stored
        if (UserBookmarkValidator.HasQueryString(info.Url))
        {
            throw new ValidationException(Strings.Message_BookmarkQueryString);
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
        bookmark.Uid = BookmarkKey(user.GetUserId(), bookmark.Type, bookmark.EntityId);

        // The same bookmark made on another device or in another tab is the same document: that it exists already is what was asked for
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

        // One request; the server deletes the rows the caller may delete, which are the own ones
        await _bookmarksDataSource.Storage.DeleteManyAsync(row => row.Type == typeName && row.EntityId == entityId);
        _bookmarked.Remove(entityId);
    }
}
