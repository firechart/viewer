using B = Flox.Shared.Resources;

namespace Flox.Shared.Branding;

/// <summary>
/// The identity of the Flox platform itself: the platform branding of every host and the default application branding of a host without its own.
/// </summary>
public static class FloxPlatformBranding
{
    private static FloxBranding? _default;

    /// <summary>
    /// The branding of the Flox platform.
    /// </summary>
    public static FloxBranding Default
    {
        get
        {
            return _default ??= Create();
        }
    }

    private static FloxBranding Create()
    {
        return new FloxBranding
        {
            Title = B.Branding_ProgramTitle,
            Subtitle = B.Branding_ProgramSubtitle,
            Description = B.Branding_ProgramDescription,
            Icon = "_content/Flox.Shared/img/flox-icon.svg",
            Logo = "_content/Flox.Shared/img/flox-logo.svg",
            TouchIcon = "_content/Flox.Shared/img/flox-apple-touch-icon.png",
            CompanyName = B.Branding_CompanyName,
            Authors = B.Branding_Authors,
            LicenseType = B.Branding_License,
            LicenseUrl = B.Branding_LicenseUrl,
            SourceCodeUrl = B.Branding_SourceCode,
            Version = FloxBranding.VersionOf(typeof(FloxPlatformBranding).Assembly)
        };
    }
}
