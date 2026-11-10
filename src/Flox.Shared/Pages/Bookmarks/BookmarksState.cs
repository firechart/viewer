using Flox.Core.Models;

namespace Flox.Shared.Pages.Bookmarks;

public class BookmarksState : GuidDataSourceState<UserBookmark>
{
    public BookmarksState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    // The newest first
    protected override void ConfigureRequest(object? key)
    {
        DataSource.Sorts.SetValue(row => row.CreatedAt, false);
    }
}
