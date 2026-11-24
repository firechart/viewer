using FireChart.Viewer.Pages;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Reports;

// The sections of a page of components (the Feeds of a summary), for what the page does with all of them: its menu, open or close every one, load
// every record (Load all, print, a page loaded at once)
internal sealed class ReportPage
{
    // Rounds of next pages of LoadAllAsync: a server whose next links never end stops there
    public const int MaxLoadAllRounds = 200;

    private readonly List<IReportFeed> _feeds = [];

    public event Action? Changed;

    public IReadOnlyList<IReportFeed> Feeds => _feeds;

    public bool LoadingAll { get; private set; }

    // The rounds of next pages the last LoadAllAsync asked for
    public int Rounds { get; private set; }

    // Every section that can be shown is open and has all its records
    public bool IsComplete => _feeds.Select(feed => feed.Section).OfType<SummarySection>().Where(section => !section.Hidden)
                                    .All(section => !section.Collapsed && section.IsLoaded && !section.IsLoading && !section.Failed && !section.HasMore);

    public void Add(IReportFeed feed)
    {
        _feeds.Add(feed);
        Changed?.Invoke();
    }

    public void Remove(IReportFeed feed)
    {
        _feeds.Remove(feed);
        Changed?.Invoke();
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    public void SetCollapsed(bool collapsed)
    {
        foreach (var feed in _feeds)
        {
            feed.SetCollapsed(collapsed);
        }

        Changed?.Invoke();
    }

    // Opens every section and loads all its records, round after round, until none has a next page; a section that fails stops there
    public async Task<bool> LoadAllAsync()
    {
        LoadingAll = true;
        Rounds = 0;
        SetCollapsed(false);
        try
        {
            for (var round = 0; round < MaxLoadAllRounds; round++)
            {
                var todo = _feeds.Where(feed => feed.Section is { Hidden: false, Failed: false } section && (!section.IsLoaded || section.HasMore)).ToList();
                if (todo.Count == 0)
                {
                    break;
                }

                Rounds++;
                await Task.WhenAll(todo.Select(feed => feed.MoreAsync()));
            }

            return IsComplete;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            LoadingAll = false;
            Changed?.Invoke();
        }
    }
}

// A section of a page that the page may open, close and fill
internal interface IReportFeed
{
    SummarySection? Section { get; }

    void SetCollapsed(bool collapsed);

    // The next pages of the section, or the first ones of the searches that failed
    Task MoreAsync();
}
