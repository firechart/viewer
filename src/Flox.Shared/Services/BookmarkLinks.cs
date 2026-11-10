using System.Globalization;
using Flox.Core.Host;
using Flox.Core.Models;
using Flox.Resources;
using Flox.Shared.Extensions;
using Radzen;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Shared.Services;

// Builds the address of a bookmark in this host from what it holds (its source and location), so a bookmark outlives a change of the routes or a
// server registered again. Several: the one that handles the bookmark answers; none: the address it was made with
public interface IBookmarkLinkResolver
{
    bool Handles(UserBookmark bookmark);

    // Null: the source of the bookmark is not here (a server that is not registered)
    Task<string?> HrefOfAsync(UserBookmark bookmark, CancellationToken cancellationToken = default);
}

[FloxService(typeof(BookmarkLinks), ServiceLifetime.Scoped)]
public sealed class BookmarkLinks(IEnumerable<IBookmarkLinkResolver> resolvers)
{
    // Null: the bookmark cannot be opened here (its source is missing); the address it was made with would lead elsewhere
    public async Task<string?> HrefOfAsync(UserBookmark bookmark, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        if (resolvers.FirstOrDefault(row => row.Handles(bookmark)) is { } resolver)
        {
            return await resolver.HrefOfAsync(bookmark, cancellationToken);
        }

        return string.IsNullOrEmpty(bookmark.SourceAddress) ? bookmark.Url : null;
    }

    // Opens the bookmark, or tells the user that its source is not here
    public async Task OpenAsync(UserBookmark bookmark, NavigationManager navigation, NotificationService notifications, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(notifications);
        if (await HrefOfAsync(bookmark, cancellationToken) is { } href)
        {
            // A route of the application ("/package/...") is below its base (/app/), not at the root of the site
            navigation.NavigateToRoute(href);
            return;
        }

        notifications.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Warning,
            Summary = string.Format(CultureInfo.CurrentCulture, Strings.Message_BookmarkSourceMissing, bookmark.SourceName ?? bookmark.SourceAddress)
        });
    }
}
