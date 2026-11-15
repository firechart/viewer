using System.ComponentModel.DataAnnotations;
using Flox.Core.Settings;

namespace Flox.Shared.Settings;

/// <summary>The appearance settings of the user.</summary>
[FloxSettings("theme")]
[Display(Name = nameof(Strings.Caption_Appearance), ResourceType = typeof(Strings), Order = 0)]
public class ThemeSettings
{
    /// <summary>Whether the dark theme is the default.</summary>
    public const bool DEFAULT_DARK = true;

    /// <summary>The color theme: light, dark or the system one.</summary>
    [Display(Name = nameof(Strings.Caption_Theme), ResourceType = typeof(Strings))]
    [Setting(Control = SettingControl.RadioList)]
    public Theme Theme { get; set; } = DEFAULT_DARK ? Theme.Dark : Theme.Light;
}
