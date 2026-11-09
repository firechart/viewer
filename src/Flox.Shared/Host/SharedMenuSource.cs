using System.Security.Claims;
using Flox.Core.Replication;

namespace Flox.Shared.Host;

// Menu Position convention: Flox.Shared's own platform menu (this class) is pinned to
// int.MaxValue so it always renders last, after every app-specific menu - apps are free to pick
// any Position to order themselves against each other.
// Items within a menu are not position-sorted - a source lists them in the order it wants shown.
public sealed class SharedMenuSource
(
    IFloxRuntime runtime,
    IServiceProviderIsService services,
    FloxOidcOptions? oidc = null
) : IFloxMenuSource
{
#if DEBUG
    private const bool DEBUG_BUILD = true;
#else
    private const bool DEBUG_BUILD = false;
#endif

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
        if (true == user.Identity?.IsAuthenticated)
        {
            // The administration is a page of the server: what the user may do there is decided by the server
            menu.Items.Add(SharedMenuItems.Admin);
            menu.Items.Add(SharedMenuItems.Bookmarks);
            menu.Items.Add(SharedMenuItems.Tasks);

            // Only where a local store replicates: it is a client that has IReplicationService
            if (services.IsService(typeof(IReplicationService)))
            {
                menu.Items.Add(SharedMenuItems.Sync);
            }
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
