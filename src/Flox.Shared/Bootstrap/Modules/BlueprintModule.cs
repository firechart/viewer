using Flox.Shared.Components;

namespace Flox.Shared.Bootstrap.Modules;

internal sealed class BlueprintModule : BootstrapModule
{
    public override void ConfigureServices()
    {
        Services.AddFloxComponents();
    }
}
