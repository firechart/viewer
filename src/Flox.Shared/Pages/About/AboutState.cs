namespace Flox.Shared.Pages.About;

public class AboutState : State
{
    public AboutState(IServiceProvider serviceProvider, IFloxBrandingProvider branding) : base(serviceProvider)
    {
        _branding = branding;
    }

    private readonly IFloxBrandingProvider _branding;

    public FloxBranding? AppBranding { get; private set; }
    public FloxBranding? PlatformBranding { get; private set; }

    protected override Task OnStartAsync(object? key)
    {
        AppBranding = _branding.App;
        // Only a separate "About platform" tab if the app actually registered its own branding -
        // otherwise it'd be the exact same tab twice.
        PlatformBranding = ReferenceEquals(_branding.App, _branding.Platform) ? null : _branding.Platform;
        IsBusy = false;
        return Task.CompletedTask;
    }
}
