namespace Flox.Core;

public static class DictionaryExtensions
{
    public static void CopyFrom<TKey, TValue>(this IDictionary<TKey, TValue> self, IDictionary<TKey, TValue> source) where TKey : notnull
    {
        self.Clear();
        foreach (var key in source.Keys)
        {
            self[key] = source[key];
        }
    }
}
