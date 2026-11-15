namespace Flox.Shared.Extensions;

/// <summary>Routes of the pages that every Flox client has.</summary>
public static class SharedRoutes
{
    /// <summary>The about page.</summary>
    public const string ABOUT = "/about";
    /// <summary>The bookmarks page.</summary>
    public const string BOOKMARKS = "/bookmark";
    /// <summary>The history page of recently visited pages.</summary>
    public const string HISTORY = "/history";
    /// <summary>The home page.</summary>
    public const string HOME = "/";
    /// <summary>Alternative address of the home page.</summary>
    public const string HOME_ALT = "/home";
    /// <summary>The sign-in page.</summary>
    public const string LOGIN = "/login";
    /// <summary>The page with the menu entries that do not fit the main menu.</summary>
    public const string MORE = "/more";
    /// <summary>The profile page of the signed-in user.</summary>
    public const string PROFILE = "/profile";
    /// <summary>The search page.</summary>
    public const string SEARCH = "/search";
    /// <summary>The settings page.</summary>
    public const string SETTINGS = "/settings";
    /// <summary>The list of background tasks.</summary>
    public const string TASKS = "/task";

    /// <summary>A background task, by its id.</summary>
    public const string TASK = "/task/{Id:guid}";

    /// <summary>Orchard's admin UI: configuration and administration are not part of the client application.</summary>
    public const string ADMIN = AppUrl.ServerRootPrefix + "Admin";
}

/// <summary>Shortcuts that give a <see cref="NavigationLink"/> to the shared pages.</summary>
public static class NavigationExtensions
{
    /// <summary>Link to the about page.</summary>
    public static NavigationLink About(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.ABOUT);
    }

    /// <summary>Link to the home page.</summary>
    public static NavigationLink Home(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.HOME);
    }

    /// <summary>Link to the search page.</summary>
    public static NavigationLink Search(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.SEARCH);
    }


    /// <summary>Link to the bookmarks page.</summary>
    public static NavigationLink Bookmarks(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.BOOKMARKS);
    }

    /// <summary>Link to the history page.</summary>
    public static NavigationLink History(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.HISTORY);
    }

    /// <summary>Link to the settings page.</summary>
    public static NavigationLink Settings(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.SETTINGS);
    }

    /// <summary>Link to the list of background tasks.</summary>
    public static NavigationLink Tasks(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.TASKS);
    }

    /// <summary>Link to the page of the background task with the given id.</summary>
    public static NavigationLink Task(this NavigationManager navigationManager, Guid id)
    {
        return new NavigationLink(navigationManager, SharedRoutes.TASK.FormatMessage(id));
    }
}
