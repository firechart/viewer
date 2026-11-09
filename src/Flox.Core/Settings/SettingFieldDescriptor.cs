using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace Flox.Core.Settings;

// Texts are resolved on access: the UI culture can change while the application runs
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

    public SettingControl Control { get; }

    public string? Description
    {
        get
        {
            return _display?.GetDescription();
        }
    }

    public string? GroupName
    {
        get
        {
            return _display?.GetGroupName();
        }
    }

    public string Label
    {
        get
        {
            return _display?.GetName() ?? Property.Name;
        }
    }

    public decimal? Maximum
    {
        get
        {
            return _range is { Maximum: IConvertible max } ? Convert.ToDecimal(max, CultureInfo.InvariantCulture) : null;
        }
    }

    public decimal? Minimum
    {
        get
        {
            return _range is { Minimum: IConvertible min } ? Convert.ToDecimal(min, CultureInfo.InvariantCulture) : null;
        }
    }

    public int Step
    {
        get
        {
            return _setting?.Step ?? 0;
        }
    }

    public string Name
    {
        get
        {
            return Property.Name;
        }
    }

    public int Order
    {
        get
        {
            return _display?.GetOrder() ?? int.MaxValue;
        }
    }

    public PropertyInfo Property { get; }

    public bool Required
    {
        get
        {
            return Property.IsDefined(typeof(RequiredAttribute));
        }
    }

    public Type UnderlyingType
    {
        get
        {
            return Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;
        }
    }

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
