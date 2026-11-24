namespace Flox.Shared.Components;

/// <summary>
/// The states of the open page that a pull down at the top of a touch screen refreshes (<see cref="State.RefreshesOnPull"/>); a <see cref="StatefulWidget{TState}"/> adds its state while it is shown, the gesture of the layout asks them to refresh.
/// </summary>
[FloxService]
public sealed class PullRefreshTargets
{
    private readonly List<State> _states = [];

    /// <summary>
    /// Raised when a state is added or removed.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The open page has something to refresh: the gesture shows its indicator.
    /// </summary>
    public bool Any => _states.Count > 0;

    /// <summary>
    /// Adds a state while the widget that shows it lives; disposing the result removes it.
    /// </summary>
    public IDisposable Add(State state)
    {
        _states.Add(state);
        Changed?.Invoke(this, EventArgs.Empty);
        return new Registration(this, state);
    }

    /// <summary>
    /// Refreshes every state of the page, all at once.
    /// </summary>
    public Task RefreshAsync()
    {
        return Task.WhenAll(_states.Distinct().ToArray().Select(state => state.OnPullRefreshAsync()));
    }

    private void Remove(State state)
    {
        if (_states.Remove(state))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class Registration(PullRefreshTargets owner, State state) : IDisposable
    {
        private PullRefreshTargets? _owner = owner;

        public void Dispose()
        {
            _owner?.Remove(state);
            _owner = null;
        }
    }
}
