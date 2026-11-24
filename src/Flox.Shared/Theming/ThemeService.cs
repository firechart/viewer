using Flox.Shared.Settings;

namespace Flox.Shared.Theming;

/// <summary>
/// Holds the theme of the application.
/// </summary>
public class ThemeService
{
    /// <summary>
    /// Whether the dark theme is applied.
    /// </summary>
    public bool IsDarkMode { get; private set; } = ThemeSettings.DEFAULT_DARK;

    /// <summary>
    /// Applies the theme the user chose; <see cref="Theme.System"/> takes the scheme of the system.
    /// </summary>
    /// <param name="settings">The settings of the theme.</param>
    /// <param name="systemDark">Whether the color scheme of the system is dark (<see cref="SystemColorScheme"/>).</param>
    public void ApplyThemeSettings(ThemeSettings settings, bool systemDark)
    {
        ArgumentNullException.ThrowIfNull(settings);
        IsDarkMode = settings.Theme switch
        {
            Theme.Dark => true,
            Theme.Light => false,
            _ => systemDark
        };
    }
}
