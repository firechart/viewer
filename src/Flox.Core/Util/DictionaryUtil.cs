namespace Flox.Core.Util;

/// <summary>
/// Helpers for dictionaries.
/// </summary>
public static class DictionaryUtil
{
    /// <summary>
    /// Returns the value of the key, or the default value when there is none.
    /// </summary>
    public static TValue? GetOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, TValue? defaultValue = default) where TKey : notnull
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Returns the value of the key, or the default value when there is none.
    /// </summary>
    public static TValue? GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue? defaultValue = default) where TKey : notnull
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Sets the value of the key, adding the key when needed.
    /// </summary>
    public static void Set<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value) where TKey : notnull
    {
        dictionary[key] = value;
    }
}
