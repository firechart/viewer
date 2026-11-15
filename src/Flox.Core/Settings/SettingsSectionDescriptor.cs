using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Flox.Core.Settings;

/// <summary>Describes one settings class: its id, scope, title and editable fields.</summary>
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

    /// <summary>The description shown with the section.</summary>
    public string? Description
    {
        get
        {
            return _display?.GetDescription();
        }
    }

    /// <summary>The editable fields in display order.</summary>
    public IReadOnlyList<SettingFieldDescriptor> Fields { get; }

    /// <summary>The storage key of the settings.</summary>
    public string Id { get; }

    /// <summary>The settings class.</summary>
    public Type ModelType { get; }

    /// <summary>Who owns the settings.</summary>
    public SettingsScope Scope { get; }

    /// <summary>The place of the section among the others; smaller is first.</summary>
    public int Order
    {
        get
        {
            return _display?.GetOrder() ?? int.MaxValue;
        }
    }

    /// <summary>The title of the section; the class name when none is set.</summary>
    public string Title
    {
        get
        {
            return _display?.GetName() ?? ModelType.Name;
        }
    }

    /// <summary>Describes a settings class.</summary>
    /// <param name="modelType">The class.</param>
    /// <returns>The descriptor, or null when the class is not a settings class.</returns>
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
