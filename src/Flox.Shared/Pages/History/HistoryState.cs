using Flox.Core.Models;

namespace Flox.Shared.Pages.History;

// The recent pages of this device (RecentVisitService keeps the newest ones), the newest first
public class HistoryState : GuidDataSourceState<UserRecent>
{
    public HistoryState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    protected override void ConfigureRequest(object? key)
    {
        DataSource.Sorts.SetValue(row => row.VisitedAt, false);
    }
}
