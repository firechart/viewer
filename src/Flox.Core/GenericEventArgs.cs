namespace Flox.Core;

public class GenericEventArgs<T> : EventArgs
{
    public GenericEventArgs() { }

    public GenericEventArgs(T value)
    {
        Value = value;
    }

    public T? Value { get; set; }
}
