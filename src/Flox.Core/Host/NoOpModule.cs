namespace Flox.Core.Host;

// Used with Bootstrapper.ReplaceModule to neutralize a module a shared base class always adds
// (e.g. ConfigurationModule, EventsConsumerModule) for a platform where it doesn't apply,
// without touching the shared base class itself.
public sealed class NoOpModule : BootstrapModule
{
    public override void ConfigureServices()
    {
    }
}
