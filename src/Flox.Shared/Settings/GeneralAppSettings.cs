using System.ComponentModel.DataAnnotations;
using Flox.Core.Settings;

namespace Flox.Shared.Settings;

[FloxSettings("general")]
[Display(Name = nameof(Strings.Caption_GeneralCategory), ResourceType = typeof(Strings), Order = 1)]
public class GeneralAppSettings
{
    [Display(Name = nameof(Strings.Caption_Language), ResourceType = typeof(Strings), Order = 1)]
    [Setting(OptionsSource = typeof(LanguageOptions))]
    public string Language { get; set; } = string.Empty;

    [Display(Name = nameof(Strings.Caption_DefaultPageSize), ResourceType = typeof(Strings), Order = 2)]
    [Range(20, 200)]
    [Setting(Control = SettingControl.Slider, Step = 10)]
    public int PageSize { get; set; } = 20;

    [Display(Name = nameof(Strings.Caption_AllowTelemetry), GroupName = nameof(Strings.Caption_DataAndPrivacy), ResourceType = typeof(Strings), Order = 3)]
    public bool AllowTelemetry { get; set; }
}
