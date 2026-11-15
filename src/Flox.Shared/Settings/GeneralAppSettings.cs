using System.ComponentModel.DataAnnotations;
using Flox.Core.Settings;

namespace Flox.Shared.Settings;

/// <summary>The general settings of the user.</summary>
[FloxSettings("general")]
[Display(Name = nameof(Strings.Caption_GeneralCategory), ResourceType = typeof(Strings), Order = 1)]
public class GeneralAppSettings
{
    /// <summary>The language of the interface; empty follows the system.</summary>
    [Display(Name = nameof(Strings.Caption_Language), ResourceType = typeof(Strings), Order = 1)]
    [Setting(OptionsSource = typeof(LanguageOptions))]
    public string Language { get; set; } = string.Empty;

    /// <summary>How many rows a list shows on a page.</summary>
    [Display(Name = nameof(Strings.Caption_DefaultPageSize), ResourceType = typeof(Strings), Order = 2)]
    [Range(20, 200)]
    [Setting(Control = SettingControl.Slider, Step = 10)]
    public int PageSize { get; set; } = 20;

    /// <summary>Whether anonymous telemetry may be sent.</summary>
    [Display(Name = nameof(Strings.Caption_AllowTelemetry), GroupName = nameof(Strings.Caption_DataAndPrivacy), ResourceType = typeof(Strings), Order = 3)]
    public bool AllowTelemetry { get; set; }
}
