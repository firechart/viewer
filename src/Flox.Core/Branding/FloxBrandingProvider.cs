namespace Flox.Core.Branding;

// Concrete, mutable-App implementation. Registered once as a singleton INSTANCE by
// FloxBootstrapper's constructor; a specialized app's own bootstrapper mutates the same instance's
// App property in its own constructor (after base(...) runs) to override branding - see
// FloxBootstrapper.Branding. Mutating the held instance directly, rather than re-registering the
// service, avoids the "last AddSingleton wins" ordering subtlety entirely.
public sealed class FloxBrandingProvider
(
    FloxBranding platform
) : IFloxBrandingProvider
{
    public FloxBranding Platform { get; } = platform;

    public FloxBranding App { get; set; } = platform;
}
