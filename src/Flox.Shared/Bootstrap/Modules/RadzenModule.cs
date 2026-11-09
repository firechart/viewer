using Radzen;

namespace Flox.Shared.Bootstrap.Modules;

internal sealed class RadzenModule : BootstrapModule
{
    public override void ConfigureServices()
    {
        Services.AddRadzenComponents();
    }
}
