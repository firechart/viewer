using Radzen;

namespace Flox.Shared.Components;

/// <summary>
/// Content of a dialog opened with <c>ShowDialogAsync</c>. Closing with <see cref="Ok"/> returns the model,
/// anything else (cancel, the close button, Esc) returns nothing, i.e. a canceled result.
/// </summary>
public class ModalDialogWidget<T> : ComponentBase where T : class
{
    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Parameter]
    public T? Model { get; set; }

    // Whether the dialog adds a record or edits one; ShowDialogAsync passes it
    [Parameter]
    public ModalDialogMode Mode { get; set; }

    [Parameter]
    public EventCallback<T?> ModelChanged { get; set; }

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    protected void Close()
    {
        DialogService.Close();
    }

    protected void Cancel()
    {
        DialogService.Close();
    }

    protected void Ok()
    {
        DialogService.Close(Model);
    }
}
