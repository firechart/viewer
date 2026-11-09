namespace Flox.Core.Host;

public interface IFloxMenu
{
    string? Text { get; set; }

    // Already in the order the module wants them rendered - the host never reorders Items.
    FloxMenuItemList Items { get; }

    // Orders this menu against every other module's menu (see SharedMenuSource - the platform
    // menu is pinned to int.MaxValue so it always renders last). Items within the menu are not
    // position-sorted - only menus are.
    int Position { get; set; }
}
