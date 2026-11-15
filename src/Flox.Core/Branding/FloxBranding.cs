using System.Reflection;
using Flox.Core.Services;

namespace Flox.Core.Branding;

/// <summary>
/// Identity of an application or of the platform: names, icons, authors, license and version. Plain data: an app whose branding varies by platform or edition decides that once, in its own bootstrapper, when it builds the instance.
/// </summary>
public sealed class FloxBranding
{
    /// <summary>
    /// Name of the application.
    /// </summary>
    public required string Title { get; init; }
    /// <summary>
    /// Short tagline.
    /// </summary>
    public string? Subtitle { get; init; }
    /// <summary>
    /// Longer description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Square application icon (SVG or PNG): browser tab, About page.
    /// </summary>
    public required string Icon { get; init; }
    /// <summary>
    /// The same icon for a light page (the header of the site in the light mode), when the tile of Icon is dark; none means Icon everywhere.
    /// </summary>
    public string? IconLight { get; init; }
    /// <summary>
    /// Horizontal logo (mark and name): navigation header, splash screen, login page, site header.
    /// </summary>
    public required string Logo { get; init; }
    /// <summary>
    /// PNG 180x180 without transparency, for iOS home screens.
    /// </summary>
    public string? TouchIcon { get; init; }

    /// <summary>
    /// Name of the company that owns the application.
    /// </summary>
    public string? CompanyName { get; init; }
    /// <summary>
    /// Authors of the application.
    /// </summary>
    public string? Authors { get; init; }
    /// <summary>
    /// Name of the license.
    /// </summary>
    public string? LicenseType { get; init; }
    /// <summary>
    /// Address of the license text.
    /// </summary>
    public string? LicenseUrl { get; init; }
    /// <summary>
    /// Address of the source code.
    /// </summary>
    public string? SourceCodeUrl { get; init; }
    /// <summary>
    /// Version shown to the user.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Returns the version of the build (MinVer, for example 0.54.0 or 0.54.1-alpha.0.3) without the commit; AssemblyVersion carries only the major part.
    /// </summary>
    public static string? VersionOf(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                             ?.InformationalVersion;
        if (string.IsNullOrEmpty(version))
        {
            return assembly.GetName()
                           .Version
                          ?.ToString(3);
        }

        var metadata = version.IndexOf('+', StringComparison.Ordinal);
        return metadata < 0 ? version : version[..metadata];
    }
}
