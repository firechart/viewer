using Microsoft.AspNetCore.Components.Routing;

namespace Flox.Services;

[FloxService(typeof(INavigationHistoryService))]
public sealed class NavigationHistoryService : IDisposable, INavigationHistoryService
{
    private readonly NavigationManager _navigationManager;
    private readonly Stack<string> _history = new Stack<string>();
    private bool _isNavigatingBack;
    private string? _currentUri;

    public NavigationHistoryService(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
        _currentUri = _navigationManager.Uri;
        _navigationManager.LocationChanged += OnLocationChanged;
    }

    public event EventHandler? HistoryChanged;

    public bool CanGoBack
    {
        get
        {
            return _history.Count > 0;
        }
    }

    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        _isNavigatingBack = true;
        var previousUri = _history.Pop();
        _navigationManager.NavigateTo(previousUri);
        NotifyHistoryChanged();
    }

    public void ClearHistory()
    {
        _history.Clear();
        NotifyHistoryChanged();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_isNavigatingBack)
        {
            _isNavigatingBack = false;
            _currentUri = e.Location;
            return;
        }

        var currentDepth = GetRouteDepth(_currentUri);
        var newDepth = GetRouteDepth(e.Location);

        if (newDepth > currentDepth)
        {
            var isRootToFirstLevelTransition = currentDepth == 0 && newDepth == 1;

            if (!isRootToFirstLevelTransition && _currentUri != null)
            {
                _history.Push(_currentUri);
            }
        }
        else if (newDepth < currentDepth)
        {
            while (_history.Count > 0 && GetRouteDepth(_history.Peek()) >= newDepth)
            {
                _history.Pop();
            }
        }
        else
        {
            while (_history.Count > 0 && GetRouteDepth(_history.Peek()) >= newDepth)
            {
                _history.Pop();
            }
        }

        _currentUri = e.Location;
        NotifyHistoryChanged();
    }

    // The depth below the base of the application, whatever path the application is served from
    private int GetRouteDepth(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return 0;
        }

        var path = _navigationManager.ToBaseRelativePath(uri);
        var end = path.IndexOfAny(['?', '#']);
        if (end >= 0)
        {
            path = path[..end];
        }

        return path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                   .Length;
    }

    private void NotifyHistoryChanged()
    {
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _navigationManager.LocationChanged -= OnLocationChanged;
    }
}
