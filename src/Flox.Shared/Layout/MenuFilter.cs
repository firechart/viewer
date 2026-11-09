using System.Security.Claims;

namespace Flox.Shared.Layout;

// The items of the menus a user may see, as nodes: what the navigation menu and the More page show
public static class MenuFilter
{
    // One entry per menu that has something to show. Top-level items are split by placement; the children follow their parent
    public static async Task<List<(IFloxMenu Menu, List<NavMenuNode> Nodes)>> BuildAsync(IFloxShell shell, IAuthorizationService authorization, ClaimsPrincipal user, FloxMenuPlacement placement)
    {
        var result = new List<(IFloxMenu Menu, List<NavMenuNode> Nodes)>();
        foreach (var menu in shell.GetMenus(user))
        {
            var nodes = await FilterAsync(authorization, user, menu.Items.Where(row => row.Placement == placement)
                                                                      .ToList());

            // A menu whose items are all hidden is hidden as well
            if (nodes.Count > 0)
            {
                result.Add((menu, nodes));
            }
        }

        return result;
    }

    private static async Task<List<NavMenuNode>> FilterAsync(IAuthorizationService authorization, ClaimsPrincipal user, IReadOnlyList<IFloxMenuItem> items)
    {
        var nodes = new List<NavMenuNode>();
        foreach (var item in items)
        {
            if (!await IsAllowedAsync(authorization, user, item))
            {
                continue;
            }

            var children = await FilterAsync(authorization, user, item.Items);

            // A group whose children are all hidden is hidden as well
            if (item.Items.Count > 0 && children.Count == 0)
            {
                continue;
            }

            nodes.Add(new NavMenuNode(item, children));
        }

        return nodes;
    }

    private static async Task<bool> IsAllowedAsync(IAuthorizationService authorization, ClaimsPrincipal user, IFloxMenuItem item)
    {
        if (string.IsNullOrEmpty(item.Policy))
        {
            return true;
        }

        var result = await authorization.AuthorizeAsync(user, item.Policy);
        return result.Succeeded;
    }
}
