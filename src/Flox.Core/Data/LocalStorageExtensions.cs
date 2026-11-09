using System.Text.Json;

namespace Flox.Core.Data;

public static class LocalStorageExtensions
{
    public static async Task<object?> GetOrDefaultAsync(this ILocalStorage localStorage, string key, Type returnType, object? defaultValue)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        ArgumentNullException.ThrowIfNull(returnType);

        var value = await localStorage.GetAsync(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        try
        {
            var result = JsonSerializer.Deserialize(value, returnType);
            return result ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    public static async Task<T> GetOrDefaultAsync<T>(this ILocalStorage localStorage, string key, T defaultValue)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        var value = await localStorage.GetAsync(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(value);
            return result ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    public static async Task SetAsync<T>(this ILocalStorage localStorage, string key, T? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (value is null || EqualityComparer<T>.Default.Equals(value, default))
        {
            await localStorage.SetAsync(key, null);
        }
        else
        {
            var json = JsonSerializer.Serialize(value);
            await localStorage.SetAsync(key, json);
        }
    }
}
