using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Flox.Services;

/// <summary>
/// Back of the app bar is the Back of the browser: it goes where the user came from, whatever the depth of the two addresses, and the browser's own
/// Back (a key, a gesture, a button of Android) agrees with it. The browser keeps the history; Blazor numbers its entries.
/// </summary>
[FloxService(typeof(INavigationHistoryService))]
public sealed class NavigationHistoryService : IDisposable, INavigationHistoryService
{
    private readonly NavigationManager _navigationManager;
    private readonly IJSRuntime _js;
    private int _index;

    /// <summary>Creates the service and starts following the navigation manager.</summary>
    public NavigationHistoryService(NavigationManager navigationManager, IJSRuntime js)
    {
        _navigationManager = navigationManager;
        _js = js;
        _navigationManager.LocationChanged += OnLocationChanged;
        _ = ReadIndexAsync();
    }

    /// <inheritdoc />
    public event EventHandler? HistoryChanged;

    /// <inheritdoc />
    /// <remarks>A page of the app before this one in the tab, and this one below a section of the menu (a section has the menu instead).</remarks>
    public bool CanGoBack => _index > 0 && GetRouteDepth(_navigationManager.Uri) > 1;

    /// <inheritdoc />
    public void GoBack()
    {
        if (CanGoBack)
        {
            _ = CallAsync("floxHistory.back");
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _ = ReadIndexAsync();
    }

    private async Task ReadIndexAsync()
    {
        try
        {
            _index = await _js.InvokeAsync<int>("floxHistory.index");
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            _index = 0;
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task CallAsync(string identifier)
    {
        try
        {
            await _js.InvokeVoidAsync(identifier);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            // No page to go back through: the app is leaving or not in a browser
        }
    }

    // The depth below the base of the application, whatever path the application is served from
    private int GetRouteDepth(string uri)
    {
        var path = _navigationManager.ToBaseRelativePath(uri);
        var end = path.IndexOfAny(['?', '#']);
        return (end >= 0 ? path[..end] : path).Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _navigationManager.LocationChanged -= OnLocationChanged;
    }
}
