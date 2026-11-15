using System.Security.Claims;

namespace Flox.Shared.Host;

/// <summary>
/// The platform menu of Flox.Shared: the account, the settings, the bookmarks and the history, the administration for a signed-in user and the
/// diagnostics in a debug build under development. Its position is <see cref="int.MaxValue"/>, so it always renders after the menus of the applications,
/// which are free to pick any position to order themselves against each other. Items within a menu are listed in the order a source wants them shown.
/// </summary>
/// <param name="runtime">The runtime of the application.</param>
/// <param name="oidc">The sign-in options; with sign-in disabled no sign-in entry is offered.</param>
public sealed class SharedMenuSource
(
    IFloxRuntime runtime,
    FloxOidcOptions? oidc = null
) : IFloxMenuSource
{
#if DEBUG
    private const bool DEBUG_BUILD = true;
#else
    private const bool DEBUG_BUILD = false;
#endif

    /// <inheritdoc />
    public IFloxMenu GetMenu(ClaimsPrincipal user)
    {
        var menu = new FloxMenu("Flox")
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

        menu.Items.Add(SharedMenuItems.Settings);

        // The bookmarks and the history are on the device: no sign-in needed
        menu.Items.Add(SharedMenuItems.Bookmarks);
        menu.Items.Add(SharedMenuItems.History);
        if (true == user.Identity?.IsAuthenticated)
        {
            // The administration is a page of the server: what the user may do there is decided by the server
            menu.Items.Add(SharedMenuItems.Admin);
            menu.Items.Add(SharedMenuItems.Tasks);
        }

        menu.Items.Add(SharedMenuItems.About);

        // The diagnostics pages are for whoever builds the platform: a debug build running in the development environment
        if (DEBUG_BUILD && runtime.IsDevelopment)
        {
            menu.Items.Add(SharedMenuItems.Diagnostics);
        }

        return menu;
    }
}
