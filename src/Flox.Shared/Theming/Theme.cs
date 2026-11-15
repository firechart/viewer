using System.ComponentModel.DataAnnotations;

namespace Flox.Shared.Theming;

/// <summary>
/// The theme the user chose.
/// </summary>
public enum Theme
{
    /// <summary>
    /// Follow the theme of the system.
    /// </summary>
    [Display(Name = nameof(Strings.Caption_ThemeSystem), ResourceType = typeof(Strings))]
    System,

    /// <summary>
    /// Light theme.
    /// </summary>
    [Display(Name = nameof(Strings.Caption_ThemeLight), ResourceType = typeof(Strings))]
    Light,

    /// <summary>
    /// Dark theme.
    /// </summary>
    [Display(Name = nameof(Strings.Caption_ThemeDark), ResourceType = typeof(Strings))]
    Dark
}
