using System.Reflection;
using System.Text.Json;
using Flox.Core.Settings;

namespace Flox.Shared.Services;

[FloxService(typeof(IAppSettingsProvider<>))]
public sealed class AppSettingsProvider<T> : IAppSettingsProvider<T>, IDisposable where T : class, new()
{
    private static readonly FloxSettingsAttribute s_attribute = FloxSettingsAttribute.Of(typeof(T));

    private readonly string _key = s_attribute.Id;
    private readonly bool _isSystem = s_attribute.Scope == SettingsScope.System;
    private readonly ILogger<AppSettingsProvider<T>> _logger;
    private readonly ISettingsStore _store;
    private readonly ISystemSettingsStore _systemStore;

    public AppSettingsProvider(ISettingsStore store, ISystemSettingsStore systemStore, ILogger<AppSettingsProvider<T>> logger)
    {
        _store = store;
        _systemStore = systemStore;
        _logger = logger;
        _store.Changed += OnStoreChanged;
    }

    public event EventHandler? SettingsChanged;

    public Type SettingsType
    {
        get
        {
            return typeof(T);
        }
    }

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
    }

    public async Task<T> GetValue()
    {
        try
        {
            var json = _isSystem ? await _systemStore.GetAsync(_key) : await _store.GetAsync(_key);
            return string.IsNullOrEmpty(json) ? new T() : JsonSerializer.Deserialize<T>(json, JsonUtil.DefaultCamelCaseOptions) ?? new T();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read the settings {Key}", _key);
            throw;
        }
    }

    public async Task<object> GetObjectAsync()
    {
        return await GetValue();
    }

    public async Task ResetAsync()
    {
        await SetAsync(null);
    }

    public async Task SaveObjectAsync(object value)
    {
        await SaveValue((T)value);
    }

    public async Task SaveValue(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        await SetAsync(JsonSerializer.Serialize(value, JsonUtil.DefaultCamelCaseOptions));
    }

    private void OnStoreChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Key == _key)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task SetAsync(string? json)
    {
        if (_isSystem)
        {
            throw new NotSupportedException($"The settings {_key} are system settings: administrators change them in the administration.");
        }

        try
        {
            await _store.SetAsync(_key, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save the settings {Key}", _key);
            throw;
        }
    }
}
