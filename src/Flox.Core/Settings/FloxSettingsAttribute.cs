using System.Reflection;

namespace Flox.Core.Settings;

// Marks a settings class. Id is the storage key and must stay stable; the section title and order come from [Display]
[AttributeUsage(AttributeTargets.Class)]
public sealed class FloxSettingsAttribute
(
    string id
) : Attribute
{
    public string Id { get; } = id;

    public SettingsScope Scope { get; init; } = SettingsScope.User;

    public static FloxSettingsAttribute Of(Type settingsType)
    {
        return settingsType.GetCustomAttribute<FloxSettingsAttribute>() ?? throw new InvalidOperationException($"{settingsType.Name} is not a settings class: add [FloxSettings].");
    }
}
