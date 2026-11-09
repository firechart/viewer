namespace Flox.Shared.Pages.BackgroundTask;

using Core.Models;
using Services;

public class BackgroundTasksState : GuidDataSourceState<BackgroundTask>
{
    public BackgroundTasksState(IServiceProvider serviceProvider, IEventsConsumerService eventsConsumerService, IHttpClientFactory httpClientFactory) : base(serviceProvider)
    {
        EventsConsumerService = eventsConsumerService;
        EventsConsumerService.OnMessage += OnMessage;
        HttpClientFactory = httpClientFactory;
    }

    public IEventsConsumerService EventsConsumerService { get; }
    public IHttpClientFactory HttpClientFactory { get; }

    protected override void ConfigureRequest(object? key)
    {
        DataSource.SetFields(row => new
        {
            row.Uid,
            row.DisplayName,
            row.Status,
            row.StartedAt,
            row.CompletedAt
        });
        DataSource.Sorts.SetValue(row => row.StartedAt!, false);
    }

    public async Task AddSimpleAsync()
    {
        try
        {
            using var client = HttpClientFactory.CreateClient("odata");
            await client.PostAsync("/odata/BackgroundTask/Simple", null);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await this.HandleExceptionUI(ex: ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CleanupAsync()
    {
        if (true == await this.ConfirmAsync(Strings.Caption_ConfirmPurgeTasks))
        {
            try
            {
                IsBusy = true;
                using var client = HttpClientFactory.CreateClient("odata");
                await client.PostAsync("/odata/BackgroundTask/Cleanup", null);
                await client.PostAsync("/odata/BackgroundTask/DeleteCompleted", null);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                await this.HandleExceptionUI(ex: ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private void OnMessage(object? sender, GenericEventArgs<EventMessage> evt)
    {
        UiErrors.Fire(() => ApplyMessageAsync(evt.Value!), nameof(OnMessage));
    }

    private async Task ApplyMessageAsync(EventMessage evt)
    {
        if (evt.PayloadType != typeof(BackgroundTask).FullName)
        {
            return;
        }

        var task = evt.Deserialize<BackgroundTask>();
        if (task != null)
        {
            var entity = DataSource.Items.FirstOrDefault(row => row.Uid == task.Uid);
            if (entity != null)
            {
                entity.Status = task.Status;
                entity.CompletedAt = task.CompletedAt;
                entity.Message = task.Message;
                entity.Error = task.Error;
                NotifyChanged();
            }
            else
            {
                await RefreshAsync();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            EventsConsumerService.OnMessage -= OnMessage;
        }
    }
}
