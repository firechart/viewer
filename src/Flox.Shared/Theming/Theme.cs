using System.ComponentModel.DataAnnotations;

namespace Flox.Shared.Theming;

public enum Theme
{
    [Display(Name = nameof(Strings.Caption_ThemeSystem), ResourceType = typeof(Strings))]
    System,

    [Display(Name = nameof(Strings.Caption_ThemeLight), ResourceType = typeof(Strings))]
    Light,

    [Display(Name = nameof(Strings.Caption_ThemeDark), ResourceType = typeof(Strings))]
    Dark
}
