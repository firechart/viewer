namespace Flox.Core.Services;

/// <summary>Tells whether the device is connected.</summary>
public interface IConnectivityService
{
    /// <summary>Whether the device is connected now.</summary>
    bool IsConnected { get; }
    /// <summary>Raised when the connection is gained or lost; the value is the new state.</summary>
    event EventHandler<GenericEventArgs<bool>>? ConnectivityChanged;
    /// <summary>Checks the connection now.</summary>
    /// <returns>True when connected.</returns>
    Task<bool> CheckConnectivityAsync();
}
