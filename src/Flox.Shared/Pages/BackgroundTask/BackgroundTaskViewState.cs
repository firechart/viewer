namespace Flox.Shared.Pages.BackgroundTask;

using Core.Models;

public class BackgroundTaskViewState : State
{
    private readonly IDataSource<Guid, BackgroundTask> _tasks;
    private readonly IHttpClientFactory _httpClientFactory;
    private Guid _id;

    public BackgroundTaskViewState(IServiceProvider serviceProvider, IDataSource<Guid, BackgroundTask> tasks, IHttpClientFactory httpClientFactory) : base(serviceProvider)
    {
        _tasks = tasks;
        _httpClientFactory = httpClientFactory;
    }

    public BackgroundTask? Item { get; private set; }

    protected override async Task OnStartAsync(object? key)
    {
        if (key is Guid id)
        {
            _id = id;
            Item = null;
            await LoadAsync();
        }
    }

    protected override async Task OnResumeAsync()
    {
        await LoadAsync();
    }

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
