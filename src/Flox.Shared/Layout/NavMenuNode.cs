namespace Flox.Shared.Layout;

/// <summary>
/// A menu item together with its already authorization-filtered children, built fresh per render so that filtering never changes the (often static, shared across users) item instances a menu source hands out.
/// </summary>
/// <param name="Item">The menu item.</param>
/// <param name="Children">The children the user may see.</param>
public sealed record NavMenuNode
(
    IFloxMenuItem Item,
    IReadOnlyList<NavMenuNode> Children
);
