namespace Flox.Core;

/// <summary>
/// Helpers for dictionaries.
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// Replaces the content of the dictionary with a copy of the source.
    /// </summary>
    public static void CopyFrom<TKey, TValue>(this IDictionary<TKey, TValue> self, IDictionary<TKey, TValue> source) where TKey : notnull
    {
        self.Clear();
        foreach (var key in source.Keys)
        {
            self[key] = source[key];
        }
    }
}
