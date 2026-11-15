namespace Flox.Core;

/// <summary>
/// Event data that carries one value.
/// </summary>
public class GenericEventArgs<T> : EventArgs
{
    /// <summary>
    /// Creates event data without a value.
    /// </summary>
    public GenericEventArgs() { }

    /// <summary>
    /// Creates event data with the value.
    /// </summary>
    public GenericEventArgs(T value)
    {
        Value = value;
    }

    /// <summary>
    /// The value the event carries.
    /// </summary>
    public T? Value { get; set; }
}
