namespace Flox.Shared.Extensions;

public static class SharedRoutes
{
    public const string ABOUT = "/about";
    public const string BOOKMARKS = "/bookmark";
    public const string HISTORY = "/history";
    public const string HOME = "/";
    public const string HOME_ALT = "/home";
    public const string LOGIN = "/login";
    public const string MORE = "/more";
    public const string PROFILE = "/profile";
    public const string SEARCH = "/search";
    public const string SETTINGS = "/settings";
    public const string SYNC = "/sync";
    public const string TASKS = "/task";

    public const string TASK = "/task/{Id:guid}";

    // Orchard's admin UI: configuration and administration are not part of the client application
    public const string ADMIN = AppUrl.ServerRootPrefix + "Admin";
}

public static class NavigationExtensions
{
    public static NavigationLink About(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.ABOUT);
    }

    public static NavigationLink Home(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.HOME);
    }

    public static NavigationLink Search(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.SEARCH);
    }


    public static NavigationLink Bookmarks(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.BOOKMARKS);
    }

    public static NavigationLink History(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.HISTORY);
    }

    public static NavigationLink Settings(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.SETTINGS);
    }

    public static NavigationLink Tasks(this NavigationManager navigationManager)
    {
        return new NavigationLink(navigationManager, SharedRoutes.TASKS);
    }

    public static NavigationLink Task(this NavigationManager navigationManager, Guid id)
    {
        return new NavigationLink(navigationManager, SharedRoutes.TASK.FormatMessage(id));
    }
}
