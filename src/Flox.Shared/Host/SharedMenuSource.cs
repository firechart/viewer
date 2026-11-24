using System.Security.Claims;
using Flox.Core.Models;
using Flox.Core.Settings;
using Flox.Shared.Services;

namespace Flox.Shared.Host;

/// <summary>
/// The platform menu of Flox.Shared: the account, the settings, the bookmarks, the history and About, the administration for a signed-in user and
/// the diagnostics in a debug build under development. Its position is <see cref="int.MaxValue"/>, so it always renders after the menus of the applications,
/// which are free to pick any position to order themselves against each other. Items within a menu are listed in the order a source wants them shown.
/// </summary>
/// <param name="runtime">The runtime of the application.</param>
/// <param name="oidc">The sign-in options; with sign-in disabled no sign-in entry is offered.</param>
/// <param name="services">What the container holds: the settings, the bookmarks and the history are offered only where their store is registered;
/// without it all three are offered.</param>
public sealed class SharedMenuSource
(
    IFloxRuntime runtime,
    FloxOidcOptions? oidc = null,
    IServiceProviderIsService? services = null
) : IFloxMenuSource
{
#if DEBUG
    private const bool DEBUG_BUILD = true;
#else
    private const bool DEBUG_BUILD = false;
#endif

    /// <summary>
    /// Whether the main menu offers the page More (the rarer entries and the diagnostics): only a debug build in the development environment.
    /// </summary>
    public static bool ShowsMore(IFloxRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return DEBUG_BUILD && runtime.IsDevelopment;
    }

    /// <inheritdoc />
    public IFloxMenu GetMenu(ClaimsPrincipal user)
    {
        // No title: the name of the platform says nothing to a user; the main menu draws a line above it
        var menu = new FloxMenu
        {
            Position = int.MaxValue
        };
        // The account first: the profile, or the sign-in (the app bar holds the menu of the page instead)
        if (true == user.Identity?.IsAuthenticated)
        {
            menu.Items.Add(SharedMenuItems.Profile);
        }
        else if (oidc?.Enabled == true)
        {
            menu.Items.Add(SharedMenuItems.Login);
        }

        // The settings of the user where the application keeps them (ISettingsStore: the server, or the device)
        if (services?.IsService(typeof(ISettingsStore)) ?? true)
        {
            menu.Items.Add(SharedMenuItems.Settings);
        }

        // The bookmarks and the history where the application keeps them (on the device: no sign-in needed)
        if (services?.HasStorage<UserBookmark>() ?? true)
        {
            menu.Items.Add(SharedMenuItems.Bookmarks);
        }

        if (services?.HasStorage<UserRecent>() ?? true)
        {
            menu.Items.Add(SharedMenuItems.History);
        }

        menu.Items.Add(SharedMenuItems.About);
        if (true == user.Identity?.IsAuthenticated)
        {
            // The administration is a page of the server: what the user may do there is decided by the server
            menu.Items.Add(SharedMenuItems.Admin);
            menu.Items.Add(SharedMenuItems.Tasks);
        }

        // The diagnostics pages are for whoever builds the platform: a debug build running in the development environment
        if (ShowsMore(runtime))
        {
            menu.Items.Add(SharedMenuItems.Diagnostics);
        }

        return menu;
    }
}
