using Flox.Core.Services;

namespace Flox.Core.Branding;

// Plain data - no platform/edition branching lives here. An app that needs its branding to vary by
// IFloxRuntime.Platform/Edition decides that once, in its own bootstrapper, when it builds the
// FloxBranding instance it hands to FloxBootstrapper.Branding.App.
public sealed class FloxBranding
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Description { get; init; }

    // Square application icon (SVG or PNG): browser tab, About page
    public required string Icon { get; init; }
    // The same icon for a light page (the header of the site in the light mode), when the tile of Icon is dark; none: Icon everywhere
    public string? IconLight { get; init; }
    // Horizontal logo (mark and name): navigation header, splash screen, login page, site header
    public required string Logo { get; init; }
    // PNG 180x180 without transparency, for iOS home screens
    public string? TouchIcon { get; init; }

    public string? CompanyName { get; init; }
    public string? Authors { get; init; }
    public string? LicenseType { get; init; }
    public string? LicenseUrl { get; init; }
    public string? SourceCodeUrl { get; init; }
    public IReadOnlyList<LicenseEntry> Licenses { get; init; } = [];
    public string? Version { get; init; }
}
