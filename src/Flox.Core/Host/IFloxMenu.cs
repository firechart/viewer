namespace Flox.Core.Host;

/// <summary>A menu of one module, shown under its own title in the navigation.</summary>
public interface IFloxMenu
{
    /// <summary>The title of the menu.</summary>
    string? Text { get; set; }

    /// <summary>The items in the order the module wants them shown; the host never reorders them.</summary>
    FloxMenuItemList Items { get; }

    /// <summary>Orders this menu against the menus of other modules; smaller is first. Items within a menu are not sorted by it.</summary>
    int Position { get; set; }
}
