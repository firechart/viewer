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
    public FloxDialogService DialogService { get; set; } = default!;

    /// <summary>
    /// The dialog that shows the widget.
    /// </summary>
    [CascadingParameter]
    public FloxDialog? Dialog { get; set; }

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
        CloseWith(null);
    }

    /// <summary>
    /// Cancels the dialog: closes it without a result.
    /// </summary>
    protected void Cancel()
    {
        CloseWith(null);
    }

    /// <summary>
    /// Closes the dialog and returns the model.
    /// </summary>
    protected void Ok()
    {
        CloseWith(Model);
    }

    private void CloseWith(object? result)
    {
        if (Dialog is not null)
        {
            DialogService.CloseDialog(Dialog, result);
        }
        else
        {
            DialogService.Close(result);
        }
    }
}
