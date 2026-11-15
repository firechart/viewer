namespace Flox.Core.Branding;

/// <summary>
/// Holds the branding of the platform and of the running application. Registered once as a singleton instance; an application's own bootstrapper replaces App on that instance.
/// </summary>
/// <param name="platform">Branding of the platform; also the initial branding of the application.</param>
public sealed class FloxBrandingProvider
(
    FloxBranding platform
) : IFloxBrandingProvider
{
    /// <summary>
    /// The platform's own identity, never overridden.
    /// </summary>
    public FloxBranding Platform { get; } = platform;

    /// <summary>
    /// The identity of the running application; equals Platform until the application's bootstrapper sets another.
    /// </summary>
    public FloxBranding App { get; set; } = platform;
}
