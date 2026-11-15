namespace Flox.Core;

/// <summary>
/// Helpers for lists.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Adds the value that the function returns.
    /// </summary>
    public static void Add<T>(this List<T> self, Func<T> f)
    {
        var value = f();
        self.Add(value);
    }
}
