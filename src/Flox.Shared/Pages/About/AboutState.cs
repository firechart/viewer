namespace Flox.Shared.Pages.About;

/// <summary>State of the About page: the identity of the application that runs.</summary>
public class AboutState : State
{
    /// <summary>Creates the state over the branding provider.</summary>
    public AboutState(IServiceProvider serviceProvider, IFloxBrandingProvider branding) : base(serviceProvider)
    {
        _branding = branding;
    }

    private readonly IFloxBrandingProvider _branding;

    /// <summary>The application that runs (the platform's own identity when the application registered none).</summary>
    public FloxBranding? Branding { get; private set; }

    /// <inheritdoc />
    protected override Task OnStartAsync(object? key)
    {
        Branding = _branding.App;
        IsBusy = false;
        return Task.CompletedTask;
    }
}
