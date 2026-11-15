#pragma warning disable CS0067

namespace Flox.Core.Services;

/// <summary>A connectivity service for hosts that are always connected.</summary>
public sealed class NullConnectivityService : ConnectivityServiceBase
{
    /// <summary>Always true.</summary>
    public override bool IsConnected
    {
        get
        {
            return true;
        }
    }

    /// <summary>Always reports a connection.</summary>
    /// <returns>True.</returns>
    public override Task<bool> CheckConnectivityAsync()
    {
        return Task.FromResult(true);
    }
}
