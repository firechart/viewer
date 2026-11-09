namespace Flox.Core.Services;

public abstract class ConnectivityServiceBase : IConnectivityService
{
    public abstract bool IsConnected { get; }

    public event EventHandler<GenericEventArgs<bool>>? ConnectivityChanged;

    public abstract Task<bool> CheckConnectivityAsync();

    protected void FireConnectivityChanged(bool connected)
    {
        ConnectivityChanged?.Invoke(this, new GenericEventArgs<bool>() { Value = connected });
    }
}
