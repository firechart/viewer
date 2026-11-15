namespace Flox.Shared.Pages.BackgroundTask;

using Core.Models;

/// <summary>State of the page of one background task: loads it and can cancel or delete it.</summary>
public class BackgroundTaskViewState : State
{
    private readonly IDataSource<Guid, BackgroundTask> _tasks;
    private readonly IHttpClientFactory _httpClientFactory;
    private Guid _id;

    /// <summary>Creates the state over the data source of tasks and the factory of HTTP clients.</summary>
    public BackgroundTaskViewState(IServiceProvider serviceProvider, IDataSource<Guid, BackgroundTask> tasks, IHttpClientFactory httpClientFactory) : base(serviceProvider)
    {
        _tasks = tasks;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>The task being shown; <c>null</c> until it is loaded.</summary>
    public BackgroundTask? Item { get; private set; }

    /// <inheritdoc />
    protected override async Task OnStartAsync(object? key)
    {
        if (key is Guid id)
        {
            _id = id;
            Item = null;
            await LoadAsync();
        }
    }

    /// <inheritdoc />
    protected override async Task OnResumeAsync()
    {
        await LoadAsync();
    }

    /// <summary>Deletes the task on the server.</summary>
    public async Task DeleteAsync()
    {
        if (Item == null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            using var client = _httpClientFactory.CreateClient("odata");
            await client.DeleteAsync($"/odata/BackgroundTask/{Item.Uid}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Asks the server to cancel the task when it is running.</summary>
    public async Task CancelAsync()
    {
        if (Item is { Status: BackgroundTaskStatus.Running })
        {
            try
            {
                IsBusy = true;
                using var client = _httpClientFactory.CreateClient("odata");
                await client.PostAsync($"/odata/BackgroundTask/Cancel/{Item.Uid}", null);
                Item.Status = BackgroundTaskStatus.Cancelled;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            Item = await _tasks.GetByIdAsync(_id);
            if (Item == null)
            {
                NotifyNotFound();
            }
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
