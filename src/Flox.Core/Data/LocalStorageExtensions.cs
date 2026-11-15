using System.Text.Json;

namespace Flox.Core.Data;

/// <summary>Typed JSON access to an <see cref="ILocalStorage"/>.</summary>
public static class LocalStorageExtensions
{
    /// <summary>Reads the value of the key as JSON of the given type; the default when it is missing or not valid JSON.</summary>
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

    /// <summary>Reads the value of the key as JSON of type <typeparamref name="T"/>; the default when it is missing or not valid JSON.</summary>
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

    /// <summary>Stores the value of the key as JSON; a null or default value removes the key.</summary>
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
