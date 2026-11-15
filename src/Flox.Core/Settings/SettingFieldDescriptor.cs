using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace Flox.Core.Settings;

/// <summary>Describes one editable setting of a settings class: its label, limits and control. Texts are resolved on access because the UI culture can change while the application runs.</summary>
public sealed class SettingFieldDescriptor
{
    private readonly DisplayAttribute? _display;
    private readonly RangeAttribute? _range;
    private readonly SettingAttribute? _setting;

    internal SettingFieldDescriptor(PropertyInfo property)
    {
        Property = property;
        _display = property.GetCustomAttribute<DisplayAttribute>();
        _range = property.GetCustomAttribute<RangeAttribute>();
        _setting = property.GetCustomAttribute<SettingAttribute>();
        Control = ResolveControl();
    }

    /// <summary>The control that edits the setting.</summary>
    public SettingControl Control { get; }

    /// <summary>The description shown with the field.</summary>
    public string? Description
    {
        get
        {
            return _display?.GetDescription();
        }
    }

    /// <summary>The name of the group the field is shown in.</summary>
    public string? GroupName
    {
        get
        {
            return _display?.GetGroupName();
        }
    }

    /// <summary>The label of the field; the property name when none is set.</summary>
    public string Label
    {
        get
        {
            return _display?.GetName() ?? Property.Name;
        }
    }

    /// <summary>The largest allowed value, if the setting has a range.</summary>
    public decimal? Maximum
    {
        get
        {
            return _range is { Maximum: IConvertible max } ? Convert.ToDecimal(max, CultureInfo.InvariantCulture) : null;
        }
    }

    /// <summary>The smallest allowed value, if the setting has a range.</summary>
    public decimal? Minimum
    {
        get
        {
            return _range is { Minimum: IConvertible min } ? Convert.ToDecimal(min, CultureInfo.InvariantCulture) : null;
        }
    }

    /// <summary>The step of a slider; 0 is the default of the control.</summary>
    public int Step
    {
        get
        {
            return _setting?.Step ?? 0;
        }
    }

    /// <summary>The name of the property.</summary>
    public string Name
    {
        get
        {
            return Property.Name;
        }
    }

    /// <summary>The place of the field among the others; smaller is first.</summary>
    public int Order
    {
        get
        {
            return _display?.GetOrder() ?? int.MaxValue;
        }
    }

    /// <summary>The property of the settings class.</summary>
    public PropertyInfo Property { get; }

    /// <summary>Whether the setting must have a value.</summary>
    public bool Required
    {
        get
        {
            return Property.IsDefined(typeof(RequiredAttribute));
        }
    }

    /// <summary>The type of the setting without <see cref="Nullable{T}"/>.</summary>
    public Type UnderlyingType
    {
        get
        {
            return Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
        }
    }

    /// <summary>Lists the values the user can choose from: those of the options source, or the members of an enum.</summary>
    /// <returns>The options; empty when there are none.</returns>
    public IReadOnlyList<SettingOption> GetOptions()
    {
        if (_setting?.OptionsSource is { } sourceType)
        {
            return ((ISettingOptionsSource)Activator.CreateInstance(sourceType)!).GetOptions();
        }

        if (UnderlyingType.IsEnum)
        {
            return UnderlyingType.GetFields(BindingFlags.Public | BindingFlags.Static)
                                 .Select(field => new SettingOption(field.GetCustomAttribute<DisplayAttribute>()
                                                                         ?.GetName() ?? field.Name,
                                                                    field.GetValue(null)))
                                 .ToList();
        }

        return [];
    }

    private SettingControl ResolveControl()
    {
        if (_setting is { Control: not SettingControl.Auto })
        {
            return _setting.Control is SettingControl.Slider && UnderlyingType != typeof(int) ? SettingControl.Number : _setting.Control;
        }

        if (_setting?.OptionsSource is not null || UnderlyingType.IsEnum)
        {
            return SettingControl.Dropdown;
        }

        if (UnderlyingType == typeof(bool))
        {
            return SettingControl.Switch;
        }

        if (UnderlyingType.IsPrimitive || UnderlyingType == typeof(decimal))
        {
            return SettingControl.Number;
        }

        return SettingControl.Text;
    }
}
