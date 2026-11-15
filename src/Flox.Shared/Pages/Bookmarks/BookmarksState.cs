using Flox.Core.Models;

namespace Flox.Shared.Pages.Bookmarks;

/// <summary>State of the list of bookmarks kept on this device.</summary>
public class BookmarksState : GuidDataSourceState<UserBookmark>
{
    /// <summary>Creates the state.</summary>
    public BookmarksState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    /// <inheritdoc />
    /// <remarks>The newest first.</remarks>
    protected override void ConfigureRequest(object? key)
    {
        DataSource.Sorts.SetValue(row => row.CreatedAt, false);
    }
}
