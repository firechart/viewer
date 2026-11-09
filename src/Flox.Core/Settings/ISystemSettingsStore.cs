namespace Flox.Core.Settings;

// The raw system settings by key, readable by everyone; they are written in the administration (SystemSettingsService on the server)
public interface ISystemSettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
}
