using System.Collections;

namespace Flox.Shared.Branding;

// A list read at the first use: the licenses of the packages (hundreds of entries) are needed by the About page only, not by every start
internal sealed class LazyList<T>(Func<IReadOnlyList<T>> load) : IReadOnlyList<T>
{
    private readonly Lazy<IReadOnlyList<T>> _items = new(load);

    public int Count
    {
        get
        {
            return _items.Value.Count;
        }
    }

    public T this[int index]
    {
        get
        {
            return _items.Value[index];
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _items.Value.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
