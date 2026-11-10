using FireChart.Viewer.Routing;
using Flox.Core;
using Flox.Core.Data;
using Flox.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer;

// The bookmarks of the Viewer in the bookmarks of the host (IGuidEntityStorage<UserBookmark>, kept on the device); a host without them has none,
// and the Viewer shows no bookmark. One page of one server is one bookmark: its key is the address of the server and the location
internal sealed class ViewerBookmarks(IGuidEntityStorage<UserBookmark> storage)
{
    public static ViewerBookmarks? Of(IServiceProvider services)
    {
        return services.GetService<IGuidEntityStorage<UserBookmark>>() is { } storage ? new ViewerBookmarks(storage) : null;
    }

    public static bool AreAvailable(IServiceProvider services)
    {
        return services.GetService<IGuidEntityStorage<UserBookmark>>() is not null;
    }

    public static Guid KeyOf(IViewerServer server, ViewerRoute route)
    {
        return UserBookmark.KeyOf(UserBookmarkTypes.Viewer, server.BaseUrl, route.Location);
    }

    public static bool IsOf(UserBookmark bookmark, IViewerServer server)
    {
        return string.Equals(UserBookmark.NormalizeAddress(bookmark.SourceAddress), UserBookmark.NormalizeAddress(server.BaseUrl), StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> ContainsAsync(IViewerServer server, ViewerRoute route)
    {
        return await storage.ExistsAsync(KeyOf(server, route));
    }

    public async Task AddAsync(ViewerSession session, ViewerRoute route, string title, string category, string? icon)
    {
        var bookmark = new UserBookmark
        {
            Uid = KeyOf(session.Server, route),
            Type = UserBookmarkTypes.Viewer,
            EntityId = route.Location,
            Location = route.Location,
            DisplayName = string.IsNullOrWhiteSpace(title) ? route.Location : title,
            Category = category,
            Icon = icon,
            Url = session.HrefOf(route),
            SourceKey = session.Server.Key,
            SourceName = session.Server.Name,
            SourceAddress = UserBookmark.NormalizeAddress(session.Server.BaseUrl)
        };

        try
        {
            await storage.InsertAsync(bookmark);
        }
        catch (DuplicateException)
        {
            // another tab has made it: that is what was asked for
        }
    }

    public async Task RemoveAsync(Guid key)
    {
        try
        {
            await storage.DeleteAsync(key);
        }
        catch (NotFoundException)
        {
            // removed already
        }
    }

    // All the bookmarks of the Viewer, of every server, the newest first
    public async Task<IReadOnlyList<UserBookmark>> ListAsync()
    {
        var request = new PagedRequest<UserBookmark>
        {
            PageSize = 0,
            ReturnTotal = false
        };
        request.Filters.SetValue(nameof(UserBookmark.Type), UserBookmarkTypes.Viewer);
        request.Sorts.SetValue(nameof(UserBookmark.CreatedAt), ascending: false);
        return (await storage.FindAsync(request)).Items;
    }
}
