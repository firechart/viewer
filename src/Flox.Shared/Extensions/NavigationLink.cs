namespace Flox.Shared.Extensions;

public sealed class NavigationLink
{
    private readonly NavigationManager? _navigationManager;

    public NavigationLink(NavigationManager? navigationManager, string relativeUrl)
    {
        _navigationManager = navigationManager;
        _relativeUrl = relativeUrl;
    }

    private readonly string _relativeUrl;

    public string ToUrl()
    {
        return _relativeUrl;
    }

    public override string ToString()
    {
        return ToUrl();
    }

    public void Navigate()
    {
        if (_navigationManager != null)
        {
            _navigationManager.NavigateToRoute(ToUrl());
        }
    }
}
