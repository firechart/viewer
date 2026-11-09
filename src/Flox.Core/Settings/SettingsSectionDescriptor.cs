using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Flox.Core.Settings;

public sealed class SettingsSectionDescriptor
{
    private readonly DisplayAttribute? _display;

    private SettingsSectionDescriptor(Type modelType, FloxSettingsAttribute attribute)
    {
        ModelType = modelType;
        Id = attribute.Id;
        Scope = attribute.Scope;
        _display = modelType.GetCustomAttribute<DisplayAttribute>();
        Fields = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                          .Where(IsEditable)
                          .Select(property => new SettingFieldDescriptor(property))
                          .OrderBy(field => field.Order)
                          .ToList();
    }

    public string? Description
    {
        get
        {
            return _display?.GetDescription();
        }
    }

    public IReadOnlyList<SettingFieldDescriptor> Fields { get; }

    public string Id { get; }

    public Type ModelType { get; }

    public SettingsScope Scope { get; }

    public int Order
    {
        get
        {
            return _display?.GetOrder() ?? int.MaxValue;
        }
    }

    public string Title
    {
        get
        {
            return _display?.GetName() ?? ModelType.Name;
        }
    }

    // Null when the type is not a settings class
    public static SettingsSectionDescriptor? Create(Type modelType)
    {
        var attribute = modelType.GetCustomAttribute<FloxSettingsAttribute>();
        return attribute is null ? null : new SettingsSectionDescriptor(modelType, attribute);
    }

    private static bool IsEditable(PropertyInfo property)
    {
        return property is { CanRead: true, CanWrite: true } && property.GetIndexParameters().Length == 0 && property.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false;
    }
}
