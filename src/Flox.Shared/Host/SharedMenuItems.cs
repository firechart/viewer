using Flox.Shared.Extensions;

namespace Flox.Core.Host;

public static class SharedMenuItems
{
    public const string NullIcon = "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g>";

    public static IFloxMenuItem Home => new FloxMenuItem
    {
        Text = Strings.Nav_Home,
        Href = SharedRoutes.HOME,
        Icon = "home"
    };

    // Not the name of the user: it is not shown on the screen
    public static IFloxMenuItem Profile => new FloxMenuItem
    {
        Text = Strings.Caption_Profile,
        Href = SharedRoutes.PROFILE,
        Icon = "account_circle"
    };

    public static IFloxMenuItem Login => new FloxMenuItem
    {
        Text = Strings.Caption_SignIn,
        Href = SharedRoutes.LOGIN,
        Icon = "login"
    };

    public static IFloxMenuItem Settings => new FloxMenuItem
    {
        Text = Strings.Settings_PageTitle,
        Href = SharedRoutes.SETTINGS,
        Icon = MaterialIcons.Gear
    };

    public static IFloxMenuItem Bookmarks => new FloxMenuItem
    {
        Text = Strings.Caption_Bookmarks,
        Href = SharedRoutes.BOOKMARKS,
        Icon = "bookmark"
    };

    public static IFloxMenuItem About => new FloxMenuItem
    {
        Text = Strings.About,
        Href = SharedRoutes.ABOUT,
        Icon = MaterialIcons.Help,
        Placement = FloxMenuPlacement.More
    };

    public static IFloxMenuItem Tasks => new FloxMenuItem
    {
        Text = Strings.Caption_BackgroundTasks,
        Href = SharedRoutes.TASKS,
        Icon = "task",
        Placement = FloxMenuPlacement.More
    };

    public static IFloxMenuItem Sync => new FloxMenuItem
    {
        Text = Strings.Sync_Title,
        Href = SharedRoutes.SYNC,
        Icon = "sync",
        Placement = FloxMenuPlacement.More
    };

    // The only administration entry of the client: a link to Orchard's own admin UI
    public static IFloxMenuItem Admin => new FloxMenuItem
    {
        Text = Strings.Caption_Admin,
        Icon = "admin_panel_settings",
        Href = SharedRoutes.ADMIN
    };

    public static IFloxMenuItem Test { get; } = new FloxMenuItem
    {
        Text = "Test",
        Href = "/test",
        Icon = MaterialIcons.Experiment
    };

    public static IFloxMenuItem Diagnostics => new FloxMenuItem
    {
        Text = Strings.Diagnostics_Title,
        Icon = MaterialIcons.Stethoscope,
        Href = "/diagnostics",
        Placement = FloxMenuPlacement.More,
        Items = new FloxMenuItemList
        {
            new FloxMenuItem(Strings.Diagnostics_Routes, href: "/diagnostics/routes"),
            new FloxMenuItem(Strings.Diagnostics_Errors, href: "/diagnostics/errors"),
            new FloxMenuItem(Strings.Diagnostics_Sheets, href: "/diagnostics/sheets"),
            new FloxMenuItem(Strings.Diagnostics_CodeEditor, href: "/diagnostics/editors"),
            new FloxMenuItem(Strings.Diagnostics_Lists, href: "/diagnostics/lists"),
            new FloxMenuItem(Strings.Caption_Theme, href: "/diagnostics/theme")
        }
    };
}
