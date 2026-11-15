namespace Flox.Shared.Pages.BackgroundTask;

using Core.Models;
using Services;

/// <summary>State of the list of background tasks: keeps it current from the push channel and offers the actions on the whole list.</summary>
public class BackgroundTasksState : GuidDataSourceState<BackgroundTask>
{
    /// <summary>Creates the state and starts listening to the push channel.</summary>
    public BackgroundTasksState(IServiceProvider serviceProvider, IEventsConsumerService eventsConsumerService, IHttpClientFactory httpClientFactory) : base(serviceProvider)
    {
        EventsConsumerService = eventsConsumerService;
        EventsConsumerService.OnMessage += OnMessage;
        HttpClientFactory = httpClientFactory;
    }

    /// <summary>The client of the push channel that reports changes of tasks.</summary>
    public IEventsConsumerService EventsConsumerService { get; }
    /// <summary>The factory of HTTP clients used to call the task endpoints.</summary>
    public IHttpClientFactory HttpClientFactory { get; }

    /// <inheritdoc />
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

    /// <summary>Starts a simple demo task on the server and reloads the list.</summary>
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

    /// <summary>After confirmation asks the server to clean up tasks and delete the completed ones, then reloads the list.</summary>
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

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            EventsConsumerService.OnMessage -= OnMessage;
        }
    }
}
