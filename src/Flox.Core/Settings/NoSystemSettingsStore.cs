namespace Flox.Core.Settings;

/// <summary>A system settings store for a client without an application server: every class of settings keeps its defaults.</summary>
public sealed class NoSystemSettingsStore : ISystemSettingsStore
{
    /// <inheritdoc />
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
