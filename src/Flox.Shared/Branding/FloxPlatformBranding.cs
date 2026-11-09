using System.Text.Json;
using B = Flox.Shared.Resources;

namespace Flox.Shared.Branding;

// The Flox platform's own identity, used as IFloxBrandingProvider.Platform (always) and as the
// IFloxBrandingProvider.App default for any host that doesn't register its own branding (Flox/
// Flox.Web/Flox.Web.Client - see src/Flox.AppServer.Shared/Docs/internal/layout.md on those being demo hosts).
public static class FloxPlatformBranding
{
    private static FloxBranding? _default;

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
            Licenses = new LazyList<LicenseEntry>(LoadLicenses),
            Version = typeof(FloxPlatformBranding).Assembly
                                                  .GetName()
                                                  .Version
                                                 ?.ToString()
        };
    }

    private static List<LicenseEntry> LoadLicenses()
    {
        var result = new List<LicenseEntry>();

        var json = ResourceUtil.LoadResourceAsString(typeof(FloxPlatformBranding).Assembly, "Flox.Shared.Licenses.licenses.json");
        if (json != null)
        {
            result.AddRange(JsonSerializer.Deserialize<List<LicenseEntry>>(json, JsonUtil.DefaultCamelCaseOptions) ?? []);
        }

        json = ResourceUtil.LoadResourceAsString(typeof(FloxPlatformBranding).Assembly, "Flox.Shared.Licenses.other-licenses.json");
        if (json != null)
        {
            result.AddRange(JsonSerializer.Deserialize<List<LicenseEntry>>(json, JsonUtil.DefaultCamelCaseOptions) ?? []);
        }

        return [.. result.OrderBy(row => row.PackageId)];
    }
}
