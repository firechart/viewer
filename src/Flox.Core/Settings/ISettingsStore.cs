namespace Flox.Core.Settings;

// Raw settings of the current user by key; typing and JSON are IAppSettingsProvider's job
public interface ISettingsStore
{
    event EventHandler<SettingsChangedEventArgs>? Changed;

    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string? json, CancellationToken cancellationToken = default);
}

public sealed class SettingsChangedEventArgs
(
    string key
) : EventArgs
{
    public string Key { get; } = key;
}
