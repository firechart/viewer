using Radzen;

namespace Flox.Shared.Components;

/// <summary>
/// A sheet is declared in markup like a component but shown by Radzen's <see cref="DialogService"/>: the content
/// is a snapshot taken when the sheet opens (call <see cref="Refresh"/> after the parent changed it).
/// </summary>
public abstract class SheetBase : ComponentBase, IFloxSheet, IDisposable
{
    private bool _isOpen;
    private bool _lastIsOpen;

    /// <summary>
    /// The dialog service that shows the sheet.
    /// </summary>
    [Inject]
    protected DialogService Dialogs { get; set; } = null!;

    /// <inheritdoc />
    [Parameter]
    public bool IsOpen { get; set; }

    /// <summary>
    /// Raised when the sheet opens or closes, with the new state; allows binding <c>IsOpen</c> two ways.
    /// </summary>
    [Parameter]
    public EventCallback<bool> IsOpenChanged { get; set; }

    /// <inheritdoc />
    [Parameter]
    public string TitleText { get; set; } = string.Empty;

    /// <inheritdoc />
    [Parameter]
    public bool ShowCloseButton { get; set; } = true;

    /// <inheritdoc />
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    [Parameter]
    public RenderFragment? HeaderCommands { get; set; }

    /// <summary>
    /// Raised after the sheet opened.
    /// </summary>
    [Parameter]
    public EventCallback OnOpened { get; set; }

    /// <summary>
    /// Raised after the sheet closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClosed { get; set; }

    /// <summary>
    /// The parameters passed to the sheet host: the content and the header commands.
    /// </summary>
    protected Dictionary<string, object?> HostParameters
    {
        get
        {
            return new Dictionary<string, object?>
            {
                [nameof(SheetHost.Content)] = ChildContent!,
                [nameof(SheetHost.Commands)] = HeaderCommands!
            };
        }
    }

    /// <summary>
    /// Shows the dialog and completes when it is closed.
    /// </summary>
    protected abstract Task ShowAsync();

    /// <summary>
    /// Closes the dialog.
    /// </summary>
    protected abstract void CloseDialog();

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // Sync from the parameter only when the parent actually changed it, so that imperative
        // OpenAsync()/CloseAsync() calls are not overwritten on re-renders
        if (IsOpen == _lastIsOpen)
        {
            return;
        }

        _lastIsOpen = IsOpen;
        if (IsOpen)
        {
            await OpenAsync();
        }
        else
        {
            await CloseAsync();
        }
    }

    /// <inheritdoc />
    public async Task OpenAsync()
    {
        if (_isOpen)
        {
            return;
        }

        _isOpen = true;
        await IsOpenChanged.InvokeAsync(true);
        await OnOpened.InvokeAsync();
        _ = ShowUntilClosedAsync();
    }

    /// <inheritdoc />
    public Task CloseAsync()
    {
        if (_isOpen)
        {
            CloseDialog();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Refreshes the content of the open sheet from the current state of the parent.
    /// </summary>
    public void Refresh()
    {
        Dialogs.Refresh();
    }

    private async Task ShowUntilClosedAsync()
    {
        try
        {
            await ShowAsync();
        }
        finally
        {
            _isOpen = false;
            await IsOpenChanged.InvokeAsync(false);
            await OnClosed.InvokeAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_isOpen)
            {
                CloseDialog();
                _isOpen = false;
            }
        }
    }
}
