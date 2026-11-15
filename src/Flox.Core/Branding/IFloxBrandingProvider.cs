namespace Flox.Core.Branding;

/// <summary>
/// Gives access to the branding of the platform and of the running application.
/// </summary>
public interface IFloxBrandingProvider
{
    /// <summary>
    /// The platform's own identity: fixed, never overridden, always non-null.
    /// </summary>
    FloxBranding Platform { get; }

    /// <summary>
    /// The running application's identity; equals Platform until the application's bootstrapper sets another.
    /// </summary>
    FloxBranding App { get; }
}
