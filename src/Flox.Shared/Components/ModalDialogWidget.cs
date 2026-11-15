using Radzen;

namespace Flox.Shared.Components;

/// <summary>
/// Content of a dialog opened with <c>ShowDialogAsync</c>. Closing with <see cref="Ok"/> returns the model,
/// anything else (cancel, the close button, Esc) returns nothing, i.e. a canceled result.
/// </summary>
public class ModalDialogWidget<T> : ComponentBase where T : class
{
    /// <summary>
    /// The dialog service that closes the dialog.
    /// </summary>
    [Inject]
    public DialogService DialogService { get; set; } = default!;

    /// <summary>
    /// The model the dialog works on.
    /// </summary>
    [Parameter]
    public T? Model { get; set; }

    /// <summary>
    /// Whether the dialog adds a record or edits one.
    /// </summary>
    [Parameter]
    public ModalDialogMode Mode { get; set; }

    /// <summary>
    /// Raised when the model is replaced.
    /// </summary>
    [Parameter]
    public EventCallback<T?> ModelChanged { get; set; }

    /// <summary>
    /// The navigation manager of the application.
    /// </summary>
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Closes the dialog without a result.
    /// </summary>
    protected void Close()
    {
        DialogService.Close();
    }

    /// <summary>
    /// Cancels the dialog: closes it without a result.
    /// </summary>
    protected void Cancel()
    {
        DialogService.Close();
    }

    /// <summary>
    /// Closes the dialog and returns the model.
    /// </summary>
    protected void Ok()
    {
        DialogService.Close(Model);
    }
}
