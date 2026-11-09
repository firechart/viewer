namespace Flox.Core;

public static class ListExtensions
{
    public static void Add<T>(this List<T> self, Func<T> f)
    {
        var value = f();
        self.Add(value);
    }
}
