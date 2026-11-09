using Flox.Core.Models;

namespace Flox.Shared.Pages.Bookmarks;

public class BookmarksState : GuidDataSourceState<UserBookmark>
{
    public BookmarksState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }
}
