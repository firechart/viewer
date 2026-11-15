using System.Reflection;
using System.Text.Json;
using Flox.Core.Settings;

namespace Flox.Shared.Services;

/// <summary>Reads and saves the settings of type <typeparamref name="T"/> as JSON in the settings store; settings of the system scope can be read but not saved here.</summary>
[FloxService(typeof(IAppSettingsProvider<>))]
public sealed class AppSettingsProvider<T> : IAppSettingsProvider<T>, IDisposable where T : class, new()
{
    private static readonly FloxSettingsAttribute s_attribute = FloxSettingsAttribute.Of(typeof(T));

    private readonly string _key = s_attribute.Id;
    private readonly bool _isSystem = s_attribute.Scope == SettingsScope.System;
    private readonly ILogger<AppSettingsProvider<T>> _logger;
    private readonly ISettingsStore _store;
    private readonly ISystemSettingsStore _systemStore;

    /// <summary>Creates the provider and starts following changes of the settings store.</summary>
    public AppSettingsProvider(ISettingsStore store, ISystemSettingsStore systemStore, ILogger<AppSettingsProvider<T>> logger)
    {
        _store = store;
        _systemStore = systemStore;
        _logger = logger;
        _store.Changed += OnStoreChanged;
    }

    /// <inheritdoc />
    public event EventHandler? SettingsChanged;

    /// <inheritdoc />
    public Type SettingsType
    {
        get
        {
            return typeof(T);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<object> GetObjectAsync()
    {
        return await GetValue();
    }

    /// <inheritdoc />
    public async Task ResetAsync()
    {
        await SetAsync(null);
    }

    /// <inheritdoc />
    public async Task SaveObjectAsync(object value)
    {
        await SaveValue((T)value);
    }

    /// <inheritdoc />
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
