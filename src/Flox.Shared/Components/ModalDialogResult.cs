namespace Flox.Shared.Components;

public class ModalDialogResult<T>
{
    internal ModalDialogResult(T? data)
    {
        Data = data;
    }

    public bool Canceled
    {
        get
        {
            return Data is null;
        }
    }

    public ModalDialogMode Mode { get; internal set; }

    public T? Data { get; }
}
