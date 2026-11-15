using Flox.Core.Models;

namespace Flox.Shared.Pages.History;

/// <summary>The recent pages of this device (<see cref="Services.RecentVisitService"/> keeps the newest ones), the newest first.</summary>
public class HistoryState : GuidDataSourceState<UserRecent>
{
    /// <summary>Creates the state.</summary>
    public HistoryState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    /// <inheritdoc />
    protected override void ConfigureRequest(object? key)
    {
        DataSource.Sorts.SetValue(row => row.VisitedAt, false);
    }
}
