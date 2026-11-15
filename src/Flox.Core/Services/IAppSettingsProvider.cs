namespace Flox.Core.Services;

/// <summary>Reads and saves one typed settings class.</summary>
public interface IAppSettingsProvider
{
    /// <summary>Raised after the settings are saved or reset.</summary>
    event EventHandler? SettingsChanged;

    /// <summary>The settings class the provider serves.</summary>
    Type SettingsType { get; }

    /// <summary>Loads the settings.</summary>
    /// <returns>The settings object.</returns>
    Task<object> GetObjectAsync();

    /// <summary>Saves the settings.</summary>
    /// <param name="value">The settings object.</param>
    Task SaveObjectAsync(object value);

    /// <summary>Resets the settings to the defaults of the class.</summary>
    Task ResetAsync();
}

/// <summary>Reads and saves one typed settings class.</summary>
/// <typeparam name="T">The settings class.</typeparam>
public interface IAppSettingsProvider<T> : IAppSettingsProvider where T : class, new()
{
    /// <summary>Loads the settings.</summary>
    /// <returns>The settings.</returns>
    Task<T> GetValue();

    /// <summary>Saves the settings.</summary>
    /// <param name="value">The settings.</param>
    Task SaveValue(T value);
}
