namespace Flox.Core.Settings;

/// <summary>Raw settings of the current user by key; typing and JSON are the job of <see cref="Flox.Core.Services.IAppSettingsProvider"/>.</summary>
public interface ISettingsStore
{
    /// <summary>Raised after a setting is changed; the argument names its key.</summary>
    event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <summary>Reads a setting.</summary>
    /// <param name="key">The key of the setting.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The JSON of the setting, or null when it is not stored.</returns>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a setting.</summary>
    /// <param name="key">The key of the setting.</param>
    /// <param name="json">The JSON of the setting; null deletes it.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    Task SetAsync(string key, string? json, CancellationToken cancellationToken = default);
}

/// <summary>Tells which setting changed.</summary>
/// <param name="key">The key of the setting.</param>
public sealed class SettingsChangedEventArgs
(
    string key
) : EventArgs
{
    /// <summary>The key of the setting that changed.</summary>
    public string Key { get; } = key;
}
