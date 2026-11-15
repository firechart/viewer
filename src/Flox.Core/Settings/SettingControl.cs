namespace Flox.Core.Settings;

/// <summary>The control that edits a setting.</summary>
public enum SettingControl
{
    /// <summary>Chosen from the type of the setting.</summary>
    Auto,
    /// <summary>A single-line text box.</summary>
    Text,
    /// <summary>A masked text box.</summary>
    Password,
    /// <summary>A multi-line text box.</summary>
    TextArea,
    /// <summary>A number box.</summary>
    Number,
    /// <summary>A slider.</summary>
    Slider,
    /// <summary>An on/off switch.</summary>
    Switch,
    /// <summary>A drop-down list.</summary>
    Dropdown,
    /// <summary>A list of radio buttons.</summary>
    RadioList
}
