namespace Flox.Core.Settings;

// What [Display], [Range] and the other DataAnnotations cannot say about the editor of a setting
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute : Attribute
{
    public SettingControl Control { get; init; }

    // An ISettingOptionsSource for a value that is not an enum
    public Type? OptionsSource { get; init; }

    // The step of a slider; 0 is the default of the control
    public int Step { get; init; }
}
