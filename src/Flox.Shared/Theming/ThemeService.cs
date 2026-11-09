using Flox.Shared.Settings;

namespace Flox.Shared.Theming;

public class ThemeService
{
    // Base Radzen themes the FloxWind overlay (Assets/Styles/site.css) is applied on
    private const string LightBaseTheme = "standard";
    private const string DarkBaseTheme = "standard-dark";

    public bool IsDarkMode { get; private set; } = ThemeSettings.DEFAULT_DARK;

    public string RadzenThemeName
    {
        get
        {
            return ThemeName(IsDarkMode);
        }
    }

    public static string ThemeName(bool dark)
    {
        return dark ? DarkBaseTheme : LightBaseTheme;
    }

    public void ApplyThemeSettings(ThemeSettings settings)
    {
        IsDarkMode = settings.Theme == Theme.Dark;
    }
}
