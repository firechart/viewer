using System.Reflection;

namespace Flox.Core.Settings;

/// <summary>Marks a settings class. The id is the storage key and must stay stable; the section title and order come from <c>[Display]</c>.</summary>
/// <param name="id">The storage key of the settings.</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FloxSettingsAttribute
(
    string id
) : Attribute
{
    /// <summary>The storage key of the settings.</summary>
    public string Id { get; } = id;

    /// <summary>Who owns the settings: the user or the system.</summary>
    public SettingsScope Scope { get; init; } = SettingsScope.User;

    /// <summary>Reads the attribute of a settings class.</summary>
    /// <param name="settingsType">The settings class.</param>
    /// <returns>The attribute.</returns>
    /// <exception cref="InvalidOperationException">The class is not marked as a settings class.</exception>
    public static FloxSettingsAttribute Of(Type settingsType)
    {
        return settingsType.GetCustomAttribute<FloxSettingsAttribute>() ?? throw new InvalidOperationException($"{settingsType.Name} is not a settings class: add [FloxSettings].");
    }
}
