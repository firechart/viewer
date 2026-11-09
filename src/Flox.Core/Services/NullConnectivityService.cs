#pragma warning disable CS0067

namespace Flox.Core.Services;

public sealed class NullConnectivityService : ConnectivityServiceBase
{
    public override bool IsConnected
    {
        get
        {
            return true;
        }
    }

    public override Task<bool> CheckConnectivityAsync()
    {
        return Task.FromResult(true);
    }
}
