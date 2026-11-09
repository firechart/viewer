using System.Globalization;
using Flox.Core.Replication;

namespace Flox.Shared.Pages.Sync;

public sealed class SyncPageState : State
{
    private readonly IReplicationService? _replication;
    private bool _subscribed;
    private int _reloads;

    public SyncPageState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _replication = serviceProvider.GetService<IReplicationService>();
    }

    public bool Available => _replication is not null;

    public ReplicationStatus? Status { get; private set; }

    public IReadOnlyList<ReplicationFailure> Failures { get; private set; } = [];

    // The types, the cursors and the revisions are for whoever builds the application
    public bool ShowTechnical { get; private set; }

    public bool CanSync => Status is { SignedIn: true, IsSyncing: false };

    public string StatusIcon
    {
        get
        {
            return Status switch
            {
                { IsSyncing: true } => "sync",
                { LastError: not null } => "sync_problem",
                { Failed: > 0 } => "sync_problem",
                { Pending: > 0 } => "cloud_upload",
                _ => "cloud_done"
            };
        }
    }

    public string StatusText
    {
        get
        {
            return Status switch
            {
                { IsSyncing: true } => Strings.Sync_StatusSyncing,
                { LastError: not null } => Strings.Sync_StatusError,
                { Pending: > 0 } status => string.Format(CultureInfo.CurrentCulture, Strings.Sync_StatusPending, status.Pending),
                { LastSyncedAt: null } => Strings.Sync_Never,
                _ => Strings.Sync_StatusSynced
            };
        }
    }

    public string? LastSyncedText
    {
        get
        {
            return Status?.LastSyncedAt is { } at ? string.Format(CultureInfo.CurrentCulture, Strings.Sync_LastSynced, at.ToLocalTime()
                                                                                                                .ToString("G", CultureInfo.CurrentCulture)) : null;
        }
    }

    protected override async Task OnStartAsync(object? key)
    {
        if (_replication is null)
        {
            IsBusy = false;
            return;
        }

        var user = await AuthenticationService.GetCurrentUserAsync();
        ShowTechnical = user.IsAdmin();
        if (!_subscribed)
        {
            _subscribed = true;
            _replication.StateChanged += OnReplicationChanged;
        }

        await ReloadAsync();
        IsBusy = false;
    }

    protected override async Task OnResumeAsync()
    {
        await ReloadAsync();
    }

    public Task SyncNowAsync()
    {
        return RunAsync(async () =>
        {
            await _replication!.SyncAsync();
            await ReloadAsync();
        });
    }

    public Task RetryAsync()
    {
        return RunAsync(async () =>
        {
            await _replication!.RetryFailedAsync();
            await ReloadAsync();
        });
    }

    private async Task ReloadAsync()
    {
        Status = await _replication!.GetStatusAsync();
        Failures = await _replication.GetFailuresAsync();
        NotifyChanged();
    }

    // A sync reports several times; the screen follows the last report
    private void OnReplicationChanged(object? sender, EventArgs e)
    {
        var turn = Interlocked.Increment(ref _reloads);
        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            if (turn != Volatile.Read(ref _reloads))
            {
                return;
            }

            try
            {
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "The sync status could not be read");
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _subscribed)
        {
            _replication!.StateChanged -= OnReplicationChanged;
        }

        base.Dispose(disposing);
    }
}
