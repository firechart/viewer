using System.ComponentModel.DataAnnotations;
using Flox.Core.Settings;

namespace Flox.Shared.Settings;

[FloxSettings("theme")]
[Display(Name = nameof(Strings.Caption_Appearance), ResourceType = typeof(Strings), Order = 0)]
public class ThemeSettings
{
    public const bool DEFAULT_DARK = true;

    [Display(Name = nameof(Strings.Caption_Theme), ResourceType = typeof(Strings))]
    [Setting(Control = SettingControl.RadioList)]
    public Theme Theme { get; set; } = DEFAULT_DARK ? Theme.Dark : Theme.Light;
}
