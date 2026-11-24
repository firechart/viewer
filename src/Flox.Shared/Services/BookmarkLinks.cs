using System.Globalization;
using Flox.Core.Host;
using Flox.Core.Models;
using Flox.Resources;
using Flox.Shared.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Flox.Shared.Services;

/// <summary>
/// Builds the address of a bookmark in this host from what it holds (its source and location), so a bookmark outlives a change of the routes or a
/// server registered again. When several resolvers exist, the one that handles the bookmark answers; with none, the address the bookmark was made with is used.
/// </summary>
public interface IBookmarkLinkResolver
{
    /// <summary>Tells whether this resolver builds the address of the bookmark.</summary>
    bool Handles(UserBookmark bookmark);

    /// <summary>Returns the address of the bookmark in this host, or <c>null</c> when its source is not here (a server that is not registered).</summary>
    Task<string?> HrefOfAsync(UserBookmark bookmark, CancellationToken cancellationToken = default);
}

/// <summary>Finds the address of a bookmark in this host and opens it.</summary>
[FloxService(typeof(BookmarkLinks), ServiceLifetime.Scoped)]
public sealed class BookmarkLinks(IEnumerable<IBookmarkLinkResolver> resolvers)
{
    /// <summary>Returns the address of the bookmark, or <c>null</c> when it cannot be opened here because its source is missing; the address it was made with would lead elsewhere.</summary>
    public async Task<string?> HrefOfAsync(UserBookmark bookmark, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        if (resolvers.FirstOrDefault(row => row.Handles(bookmark)) is { } resolver)
        {
            return await resolver.HrefOfAsync(bookmark, cancellationToken);
        }

        return string.IsNullOrEmpty(bookmark.SourceAddress) ? bookmark.Url : null;
    }

    /// <summary>Opens the bookmark, or tells the user that its source is not here.</summary>
    public async Task OpenAsync(UserBookmark bookmark, NavigationManager navigation, FloxSnackbar notifications, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(notifications);
        if (await HrefOfAsync(bookmark, cancellationToken) is { } href)
        {
            // A route of the application ("/package/...") is below its base (/app/), not at the root of the site
            navigation.NavigateToRoute(href);
            return;
        }

        notifications.Show(string.Format(CultureInfo.CurrentCulture, Strings.Message_BookmarkSourceMissing, bookmark.SourceName ?? bookmark.SourceAddress), SnackSeverity.Warning);
    }
}
