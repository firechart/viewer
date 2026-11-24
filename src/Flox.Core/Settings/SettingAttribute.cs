namespace Flox.Core.Settings;

/// <summary>Says what <c>[Display]</c>, <c>[Range]</c> and the other data annotations cannot about the editor of a setting.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute : Attribute
{
    /// <summary>The control that edits the setting.</summary>
    public SettingControl Control { get; init; }

    /// <summary>An <see cref="ISettingOptionsSource"/> that lists the values of a setting that is not an enum.</summary>
    public Type? OptionsSource { get; init; }

    /// <summary>The step of a slider; 0 is the default of the control.</summary>
    public int Step { get; init; }

    /// <summary>A key of the configuration that must be <c>true</c> for the setting to be shown (a feature the application may not have); always shown when null.</summary>
    public string? ShownWhen { get; init; }
}
