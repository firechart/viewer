namespace Flox.Shared.Extensions;

/// <summary>A route of the application together with the navigation manager that can follow it.</summary>
public sealed class NavigationLink
{
    private readonly NavigationManager? _navigationManager;

    /// <summary>Creates a link to <paramref name="relativeUrl"/>; with no navigation manager it can only be read.</summary>
    public NavigationLink(NavigationManager? navigationManager, string relativeUrl)
    {
        _navigationManager = navigationManager;
        _relativeUrl = relativeUrl;
    }

    private readonly string _relativeUrl;

    /// <summary>The route of the link.</summary>
    public string ToUrl()
    {
        return _relativeUrl;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return ToUrl();
    }

    /// <summary>Navigates to the route of the link, if there is a navigation manager.</summary>
    public void Navigate()
    {
        if (_navigationManager != null)
        {
            _navigationManager.NavigateToRoute(ToUrl());
        }
    }
}
