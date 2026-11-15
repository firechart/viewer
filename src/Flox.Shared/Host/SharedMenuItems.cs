using Flox.Shared.Extensions;

namespace Flox.Core.Host;

/// <summary>Menu entries of the pages that every Flox client has.</summary>
public static class SharedMenuItems
{
    /// <summary>SVG content of an empty icon.</summary>
    public const string NullIcon = "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g>";

    /// <summary>Entry of the home page.</summary>
    public static IFloxMenuItem Home => new FloxMenuItem
    {
        Text = Strings.Nav_Home,
        Href = SharedRoutes.HOME,
        Icon = "home"
    };

    /// <summary>Entry of the profile page, captioned with a fixed text rather than the name of the user.</summary>
    public static IFloxMenuItem Profile => new FloxMenuItem
    {
        Text = Strings.Caption_Profile,
        Href = SharedRoutes.PROFILE,
        Icon = "account_circle"
    };

    /// <summary>Entry of the sign-in page.</summary>
    public static IFloxMenuItem Login => new FloxMenuItem
    {
        Text = Strings.Caption_SignIn,
        Href = SharedRoutes.LOGIN,
        Icon = "login"
    };

    /// <summary>Entry of the settings page.</summary>
    public static IFloxMenuItem Settings => new FloxMenuItem
    {
        Text = Strings.Settings_PageTitle,
        Href = SharedRoutes.SETTINGS,
        Icon = MaterialIcons.Gear
    };

    /// <summary>Entry of the bookmarks page.</summary>
    public static IFloxMenuItem Bookmarks => new FloxMenuItem
    {
        Text = Strings.Caption_Bookmarks,
        Href = SharedRoutes.BOOKMARKS,
        Icon = "bookmark"
    };

    /// <summary>Entry of the history page.</summary>
    public static IFloxMenuItem History => new FloxMenuItem
    {
        Text = Strings.Caption_History,
        Href = SharedRoutes.HISTORY,
        Icon = "history"
    };

    /// <summary>Entry of the about page.</summary>
    public static IFloxMenuItem About => new FloxMenuItem
    {
        Text = Strings.About,
        Href = SharedRoutes.ABOUT,
        Icon = MaterialIcons.Help,
        Placement = FloxMenuPlacement.More
    };

    /// <summary>Entry of the list of background tasks.</summary>
    public static IFloxMenuItem Tasks => new FloxMenuItem
    {
        Text = Strings.Caption_BackgroundTasks,
        Href = SharedRoutes.TASKS,
        Icon = "task",
        Placement = FloxMenuPlacement.More
    };

    /// <summary>The only administration entry of the client: a link to Orchard's own admin UI.</summary>
    public static IFloxMenuItem Admin => new FloxMenuItem
    {
        Text = Strings.Caption_Admin,
        Icon = "admin_panel_settings",
        Href = SharedRoutes.ADMIN
    };

    /// <summary>Entry of a test page.</summary>
    public static IFloxMenuItem Test { get; } = new FloxMenuItem
    {
        Text = "Test",
        Href = "/test",
        Icon = MaterialIcons.Experiment
    };

    /// <summary>Entry with the diagnostic pages of the platform.</summary>
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
