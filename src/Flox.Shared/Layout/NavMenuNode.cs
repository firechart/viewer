namespace Flox.Shared.Layout;

// An item together with its already authorization-filtered children, built fresh per render so
// filtering never mutates the (often static, shared-across-users) IFloxMenuItem instances a menu
// source hands out.
public sealed record NavMenuNode
(
    IFloxMenuItem Item,
    IReadOnlyList<NavMenuNode> Children
);
