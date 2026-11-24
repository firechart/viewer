namespace Flox.Shared.Components;

/// <summary>
/// Where a dialog of <see cref="FloxDialogService"/> stands.
/// </summary>
public enum FloxDialogPlacement
{
    /// <summary>
    /// In the middle of the screen.
    /// </summary>
    Center,

    /// <summary>
    /// A sheet docked to the bottom edge.
    /// </summary>
    Bottom
}

/// <summary>
/// The look and behaviour of a dialog of <see cref="FloxDialogService"/>.
/// </summary>
public sealed record FloxDialogOptions
{
    /// <summary>
    /// The width as a CSS size; never wider than the screen.
    /// </summary>
    public string? Width { get; init; }

    /// <summary>
    /// The height as a CSS size; without it the dialog is as high as its content, never higher than the screen.
    /// </summary>
    public string? Height { get; init; }

    /// <summary>
    /// Shows the close button in the corner.
    /// </summary>
    public bool ShowClose { get; init; } = true;

    /// <summary>
    /// Shows the title above the content.
    /// </summary>
    public bool ShowTitle { get; init; } = true;

    /// <summary>
    /// Closes the dialog without a result on Escape.
    /// </summary>
    public bool CloseOnEscape { get; init; } = true;

    /// <summary>
    /// Closes the dialog without a result on a click outside it.
    /// </summary>
    public bool CloseOnOverlayClick { get; init; }

    /// <summary>
    /// Takes the whole screen on a phone.
    /// </summary>
    public bool FullScreenOnPhone { get; init; }

    /// <summary>
    /// Where the dialog stands.
    /// </summary>
    public FloxDialogPlacement Placement { get; init; } = FloxDialogPlacement.Center;
}

/// <summary>
/// An open dialog of <see cref="FloxDialogService"/>: what it shows and how it ends.
/// </summary>
public sealed class FloxDialog
{
    private readonly TaskCompletionSource<object?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal FloxDialog(Type component, string title, IDictionary<string, object?> parameters, FloxDialogOptions options)
    {
        Component = component;
        Title = title;
        Parameters = parameters.ToDictionary(pair => pair.Key, pair => pair.Value!);
        Options = options;
    }

    /// <summary>
    /// The identity of the dialog.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// The type of the component shown in the dialog.
    /// </summary>
    public Type Component { get; }

    /// <summary>
    /// The title of the dialog.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// The parameters of the component.
    /// </summary>
    public IDictionary<string, object> Parameters { get; }

    /// <summary>
    /// The look and behaviour of the dialog.
    /// </summary>
    public FloxDialogOptions Options { get; }

    /// <summary>
    /// The result of the dialog: what it was closed with, <c>null</c> when it was dismissed.
    /// </summary>
    public Task<object?> Result
    {
        get
        {
            return _result.Task;
        }
    }

    internal void Complete(object? result)
    {
        _result.TrySetResult(result);
    }
}

/// <summary>
/// Opens components as modal dialogs and sheets of Blazor Blueprint, one over the other; <see cref="FloxDialogHost"/> shows them.
/// </summary>
public sealed class FloxDialogService
{
    private readonly List<FloxDialog> _dialogs = [];

    /// <summary>
    /// Raised when a dialog opens or closes, or when the open ones are to be drawn again.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The open dialogs, the topmost last.
    /// </summary>
    public IReadOnlyList<FloxDialog> Dialogs
    {
        get
        {
            return _dialogs;
        }
    }

    /// <summary>
    /// Opens the component in a dialog in the middle of the screen.
    /// </summary>
    /// <returns>What the dialog was closed with; <c>null</c> when it was dismissed.</returns>
    public Task<object?> OpenAsync<TComponent>(string title, IDictionary<string, object?>? parameters = null, FloxDialogOptions? options = null) where TComponent : IComponent
    {
        var dialog = new FloxDialog(typeof(TComponent), title, parameters ?? new Dictionary<string, object?>(), options ?? new FloxDialogOptions());
        _dialogs.Add(dialog);
        Changed?.Invoke(this, EventArgs.Empty);
        return dialog.Result;
    }

    /// <summary>
    /// Opens the component in a sheet docked to the bottom edge.
    /// </summary>
    /// <returns>What the sheet was closed with; <c>null</c> when it was dismissed.</returns>
    public Task<object?> OpenSideAsync<TComponent>(string title, IDictionary<string, object?>? parameters = null, FloxDialogOptions? options = null) where TComponent : IComponent
    {
        return OpenAsync<TComponent>(title, parameters, (options ?? new FloxDialogOptions()) with { Placement = FloxDialogPlacement.Bottom });
    }

    /// <summary>
    /// Closes the topmost dialog with a result.
    /// </summary>
    public void Close(object? result = null)
    {
        if (_dialogs.Count > 0)
        {
            CloseDialog(_dialogs[^1], result);
        }
    }

    /// <summary>
    /// Closes the dialog with a result.
    /// </summary>
    public void CloseDialog(FloxDialog dialog, object? result = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        if (_dialogs.Remove(dialog))
        {
            dialog.Complete(result);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Draws the open dialogs again: their content follows the current state of the components that opened them.
    /// </summary>
    public void Refresh()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
