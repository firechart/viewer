namespace Flox.Core.Services;

/// <summary>Base class of connectivity services: keeps the event and leaves the check to the platform.</summary>
public abstract class ConnectivityServiceBase : IConnectivityService
{
    /// <summary>Whether the device is connected now.</summary>
    public abstract bool IsConnected { get; }

    /// <summary>Raised when the connection is gained or lost; the value is the new state.</summary>
    public event EventHandler<GenericEventArgs<bool>>? ConnectivityChanged;

    /// <summary>Checks the connection now.</summary>
    /// <returns>True when connected.</returns>
    public abstract Task<bool> CheckConnectivityAsync();

    /// <summary>Raises <see cref="ConnectivityChanged"/>.</summary>
    /// <param name="connected">The new state.</param>
    protected void FireConnectivityChanged(bool connected)
    {
        ConnectivityChanged?.Invoke(this, new GenericEventArgs<bool>() { Value = connected });
    }
}
