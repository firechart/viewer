namespace Flox.Shared.Components;

public abstract partial class StatefulWidget<TState> : StatelessWidget, IDisposable where TState : State
{
    private bool _activated;
    private object? _activatedKey;

    // The state is resolved in SetParametersAsync, not injected: a failure of its constructor (a missing service)
    // in the injection happens while the parent renders, where no error boundary reaches it
    [Inject]
    protected IServiceProvider StateServices { get; set; } = null!;

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

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        // Async on purpose: a failure here becomes a faulted task, which the error boundary handles
        State ??= StateServices.GetRequiredService<TState>();
        await base.SetParametersAsync(parameters);
    }

    // The scroll position of the state: give it to a scrolling component (a list), which keeps it while the user scrolls and restores it when the page returns
    protected ScrollMemory Scroll => State.Scroll;

    // What this widget shows, as a value with equality (a record of the route values); null when there is nothing to tell
    protected virtual object? ActivationKey
    {
        get
        {
            return null;
        }
    }

    // The parameters are set on every render of the parent: the state is activated only when the key is another one
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

    // True from the start of the activation to the end of OnActivatedAsync (the crumbs of the page are made there): the page shows a skeleton of them, not the old ones
    protected bool Starting { get; private set; }

    protected virtual Task OnActivatedAsync()
    {
        return Task.CompletedTask;
    }

    private void State_OnChanged(object? sender, EventArgs e)
    {
        UiErrors.Fire(() => InvokeAsync(StateHasChanged), nameof(State_OnChanged));
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
            State?.Changed -= State_OnChanged;
        }
    }
}
