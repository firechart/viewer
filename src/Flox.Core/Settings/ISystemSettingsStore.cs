namespace Flox.Core.Settings;

/// <summary>The raw system settings by key, readable by everyone; they are written in the administration.</summary>
public interface ISystemSettingsStore
{
    /// <summary>Reads a system setting.</summary>
    /// <param name="key">The key of the setting.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The JSON of the setting, or null when it is not stored.</returns>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
}
