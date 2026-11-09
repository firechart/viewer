using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Flox.Shared.Errors;
using Microsoft.JSInterop;
using Radzen;

namespace Flox.Shared.Components;

/// <summary>
/// Base of Flox components. Besides the common services it routes every exception thrown by an event handler
/// of the component (<c>@onclick</c> and the like) to <see cref="IUiExceptionHandler"/>, so a failed action
/// shows a message instead of replacing the page with the error boundary.
/// </summary>
public class StatelessWidget : ComponentBase, IHandleEvent /* optimizes repaints */
{
    [Inject]
    protected IJSRuntime JS { get; set; } = null!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    public DialogService DialogService { get; set; } = null!;

    [Inject]
    public NotificationService Notifications { get; set; } = null!;

    [Inject]
    protected IHttpClientFactory HttpClientFactory { get; set; } = default!;

    [Inject]
    public IAuthenticationService AuthenticationService { get; set; } = default!;

    [Inject]
    public IUiExceptionHandler UiErrors { get; set; } = default!;

    [Inject]
    protected ILogger<StatelessWidget> Logger { get; set; } = default!;

    [Parameter]
#pragma warning disable CA2227
    public FloxMenuItemList Breadcrumbs { get; set; } = [];
#pragma warning restore CA2227

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (Breadcrumbs.Count == 0)
        {
            RebuildBreadcrumbs();
        }
    }

    // A page reused for other route values (a link to another item) makes its crumbs again
    protected void RebuildBreadcrumbs()
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(href: "/", icon: "home");
        InitBreadcrumbs();
    }


    // Same contract as ComponentBase: re-render after the event and again when an async handler ends
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        var task = InvokeGuardedAsync(callback, arg);
        var shouldAwait = task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled;

        StateHasChanged();
        return shouldAwait ? RenderAfterAsync(task) : Task.CompletedTask;
    }

    private async Task InvokeGuardedAsync(EventCallbackWorkItem callback, object? arg)
    {
        try
        {
            await callback.InvokeAsync(arg);
        }
        catch (Exception ex)
        {
            await UiErrors.HandleAsync(ex, GetType().Name);
        }
    }

    private async Task RenderAfterAsync(Task task)
    {
        await task;
        StateHasChanged();
    }

    /// <summary>
    /// Runs <paramref name="action"/> and shows what it throws through <see cref="UiErrors"/>.
    /// </summary>
    /// <returns><c>true</c> when it completed, <c>false</c> when it failed.</returns>
    protected Task<bool> TryAsync(Func<Task> action, string? operation = null, [CallerMemberName] string? caller = null)
    {
        return UiErrors.TryAsync(action, operation ?? caller);
    }

    /// <summary>
    /// Use the Breadcrumbs variable to populate items
    /// </summary>
    protected virtual void InitBreadcrumbs()
    {
    }
}
