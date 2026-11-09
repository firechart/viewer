namespace Flox.Core.Branding;

// Deliberately separate from IFloxModule/IFloxShell: branding needs exactly one unambiguous owner
// per app, which IFloxModule.Main/IFloxShell.MainModule never actually got (no module in this repo
// ever sets Main=true, so MainModule is always null at runtime - see src/Flox.AppServer.Shared/Docs/internal).
public interface IFloxBrandingProvider
{
    // The Flox platform's own identity - fixed, never overridden. Always non-null, so "About
    // platform" and a splash/favicon fallback always have something valid to show.
    FloxBranding Platform { get; }

    // The running app's identity. Equals Platform until a specialized app's own bootstrapper sets
    // FloxBootstrapper.Branding.App to something else.
    FloxBranding App { get; }
}
