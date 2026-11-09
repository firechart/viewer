namespace Flox.Shared.Pages.BackgroundTask;

using Core.Models;

public class BackgroundTaskLogState
(
    IServiceProvider serviceProvider
) : GuidDataSourceState<BackgroundTaskLogEntry>(serviceProvider)
{
    protected override void ConfigureRequest(object? key)
    {
        if (key is Guid taskId)
        {
            DataSource.Filters.SetValue(row => row.BackgroundTaskId == taskId);
        }

        DataSource.Sorts.SetValue(row => row.DateTime, false);
    }
}
