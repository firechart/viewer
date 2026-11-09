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

    [Inject]
    protected DialogService Dialogs { get; set; } = null!;

    [Parameter]
    public bool IsOpen { get; set; }

    [Parameter]
    public EventCallback<bool> IsOpenChanged { get; set; }

    [Parameter]
    public string TitleText { get; set; } = string.Empty;

    [Parameter]
    public bool ShowCloseButton { get; set; } = true;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // Extra commands shown above the content, aligned to the end
    [Parameter]
    public RenderFragment? HeaderCommands { get; set; }

    [Parameter]
    public EventCallback OnOpened { get; set; }

    [Parameter]
    public EventCallback OnClosed { get; set; }

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

    // Opens the dialog and completes when it is closed
    protected abstract Task ShowAsync();

    protected abstract void CloseDialog();

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

    public Task CloseAsync()
    {
        if (_isOpen)
        {
            CloseDialog();
        }

        return Task.CompletedTask;
    }

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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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
