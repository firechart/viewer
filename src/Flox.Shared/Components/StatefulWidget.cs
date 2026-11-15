namespace Flox.Shared.Components;

/// <summary>
/// A component that shows a <typeparamref name="TState"/>: it takes the state from the service provider, keeps itself rendered when the state changes and activates the state with the key of what it shows.
/// </summary>
public abstract partial class StatefulWidget<TState> : StatelessWidget, IDisposable where TState : State
{
    private bool _activated;
    private object? _activatedKey;

    /// <summary>
    /// The service provider the state is resolved from.
    /// </summary>
    [Inject]
    protected IServiceProvider StateServices { get; set; } = null!;

    /// <summary>
    /// The state the widget shows.
    /// </summary>
    public TState State
    {
        get;
        set
        {
            if (field != null)
            {
                field.Changed -= State_OnChanged;
            }

            field = value;
            field.Changed += State_OnChanged;
        }
    } = null!;

    /// <inheritdoc />
    public override async Task SetParametersAsync(ParameterView parameters)
    {
        // Async on purpose: a failure here becomes a faulted task, which the error boundary handles
        State ??= StateServices.GetRequiredService<TState>();
        await base.SetParametersAsync(parameters);
    }

    /// <summary>
    /// The scroll position of the state: given to a scrolling component, which keeps it while the user scrolls and restores it when the page returns.
    /// </summary>
    protected ScrollMemory Scroll => State.Scroll;

    /// <summary>
    /// What this widget shows, as a value with equality (a record of the route values); null when there is nothing to tell.
    /// </summary>
    protected virtual object? ActivationKey
    {
        get
        {
            return null;
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var key = ActivationKey;
        if (_activated && Equals(_activatedKey, key))
        {
            return;
        }

        _activated = true;
        _activatedKey = key;
        Starting = true;
        try
        {
            await State.ActivateAsync(key);
            await OnActivatedAsync();
        }
        finally
        {
            Starting = false;
        }
    }

    /// <summary>
    /// True from the start of the activation to the end of <see cref="OnActivatedAsync"/> (the crumbs of the page are made there): the page shows a skeleton of them, not the old ones.
    /// </summary>
    protected bool Starting { get; private set; }

    /// <summary>
    /// Called after the state was activated for the key; the page builds its breadcrumbs here.
    /// </summary>
    protected virtual Task OnActivatedAsync()
    {
        return Task.CompletedTask;
    }

    private void State_OnChanged(object? sender, EventArgs e)
    {
        UiErrors.Fire(() => InvokeAsync(StateHasChanged), nameof(State_OnChanged));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources of the widget; a derived widget overrides it.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            State?.Changed -= State_OnChanged;
        }
    }
}
