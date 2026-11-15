using Flox.Shared.Settings;

namespace Flox.Shared.Theming;

/// <summary>
/// Holds the theme of the application and maps it to the Radzen base theme.
/// </summary>
public class ThemeService
{
    // Base Radzen themes the FloxWind overlay (Assets/Styles/site.css) is applied on
    private const string LightBaseTheme = "standard";
    private const string DarkBaseTheme = "standard-dark";

    /// <summary>
    /// Whether the dark theme is applied.
    /// </summary>
    public bool IsDarkMode { get; private set; } = ThemeSettings.DEFAULT_DARK;

    /// <summary>
    /// The name of the Radzen base theme for the current mode.
    /// </summary>
    public string RadzenThemeName
    {
        get
        {
            return ThemeName(IsDarkMode);
        }
    }

    /// <summary>
    /// Returns the name of the Radzen base theme for the dark or light mode.
    /// </summary>
    public static string ThemeName(bool dark)
    {
        return dark ? DarkBaseTheme : LightBaseTheme;
    }

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
