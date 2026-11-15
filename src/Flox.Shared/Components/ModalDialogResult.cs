namespace Flox.Shared.Components;

/// <summary>
/// The result of a modal dialog: the model it returned, or a cancellation.
/// </summary>
public class ModalDialogResult<T>
{
    internal ModalDialogResult(T? data)
    {
        Data = data;
    }

    /// <summary>
    /// Whether the dialog was canceled, that is returned no data.
    /// </summary>
    public bool Canceled
    {
        get
        {
            return Data is null;
        }
    }

    /// <summary>
    /// Whether the dialog added a record or edited one.
    /// </summary>
    public ModalDialogMode Mode { get; internal set; }

    /// <summary>
    /// The model the dialog returned; null when it was canceled.
    /// </summary>
    public T? Data { get; }
}
