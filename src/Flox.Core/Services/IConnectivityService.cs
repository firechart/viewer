namespace Flox.Core.Services;

public interface IConnectivityService
{
    bool IsConnected { get; }
    event EventHandler<GenericEventArgs<bool>>? ConnectivityChanged;
    Task<bool> CheckConnectivityAsync();
}
