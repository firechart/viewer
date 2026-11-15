namespace Flox.Shared.Pages.BackgroundTask;

using Core.Models;

/// <summary>State of the log of one background task: its entries, the newest first.</summary>
public class BackgroundTaskLogState
(
    IServiceProvider serviceProvider
) : GuidDataSourceState<BackgroundTaskLogEntry>(serviceProvider)
{
    /// <inheritdoc />
    protected override void ConfigureRequest(object? key)
    {
        if (key is Guid taskId)
        {
            DataSource.Filters.SetValue(row => row.BackgroundTaskId == taskId);
        }

        DataSource.Sorts.SetValue(row => row.DateTime, false);
    }
}
