using Microsoft.AspNetCore.Components.Routing;

namespace Flox.Services;

/// <summary>
/// Whether the app is on its way to a page: from the moment the user asked (Back, a link, the browser's Back) until the new page is drawn and none of
/// its <c>BusyOverlay</c> is still loading. The app bar shows it as a thin bar under itself.
/// </summary>
[FloxService(typeof(NavigationProgress))]
public sealed class NavigationProgress : IDisposable
{
    // The time the new page has to draw itself and say that it loads
    private static readonly TimeSpan s_settle = TimeSpan.FromMilliseconds(150);

    private readonly NavigationManager _navigationManager;
    private readonly HashSet<object> _busy = [];
    private int _step;

    /// <summary>Creates the progress and starts following the navigation manager.</summary>
    public NavigationProgress(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
        _navigationManager.LocationChanged += OnLocationChanged;
    }

    /// <summary>Whether the app is on its way to a page.</summary>
    public bool Active { get; private set; }

    /// <summary>Raised when <see cref="Active"/> changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Starts the progress before the address changes (Back of the app bar), so the press is answered at once.</summary>
    public void Start()
    {
        var step = ++_step;
        SetActive(true);
        _ = SettleAsync(step);
    }

    /// <summary>Tells that a part of a page loads or no longer loads; the progress of a navigation lasts while any part loads.</summary>
    /// <param name="part">The part (a component), the same object both times.</param>
    /// <param name="busy">Whether it loads.</param>
    public void SetBusy(object part, bool busy)
    {
        var changed = busy ? _busy.Add(part) : _busy.Remove(part);
        if (changed && !busy && Active)
        {
            _ = SettleAsync(_step);
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        Start();
    }

    private async Task SettleAsync(int step)
    {
        await Task.Delay(s_settle);
        if (step == _step && _busy.Count == 0)
        {
            SetActive(false);
        }
    }

    private void SetActive(bool active)
    {
        if (Active != active)
        {
            Active = active;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _navigationManager.LocationChanged -= OnLocationChanged;
    }
}
