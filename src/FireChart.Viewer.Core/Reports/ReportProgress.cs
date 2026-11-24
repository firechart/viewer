namespace FireChart.Viewer.Core.Reports;

// How far the loading of a report is: the blocks that waited for data and those that have it, and what the searches brought. A report shown only
// when everything is loaded watches it behind its overlay; Changed may be raised on any thread
internal sealed class ReportProgress
{
    private readonly Lock _lock = new();
    private readonly List<ReportLoaded> _messages = [];
    private int _started;
    private int _finished;

    public event Action? Changed;

    public int Started
    {
        get
        {
            lock (_lock)
            {
                return _started;
            }
        }
    }

    public int Finished
    {
        get
        {
            lock (_lock)
            {
                return _finished;
            }
        }
    }

    public bool IsLoading => Finished < Started;

    // The latest what was loaded, newest last
    public IReadOnlyList<ReportLoaded> Messages
    {
        get
        {
            lock (_lock)
            {
                return [.. _messages];
            }
        }
    }

    public void Begin()
    {
        lock (_lock)
        {
            _started++;
        }

        Changed?.Invoke();
    }

    public void End()
    {
        lock (_lock)
        {
            _finished++;
        }

        Changed?.Invoke();
    }

    public void Loaded(string type, int count)
    {
        lock (_lock)
        {
            _messages.Add(new ReportLoaded(type, count));
        }

        Changed?.Invoke();
    }
}

// A search that answered: the type of its resources and how many
internal sealed record ReportLoaded(string Type, int Count);
