namespace Flox.Core.Host;

/// <summary>A menu of one module, shown under its own title in the navigation.</summary>
public class FloxMenu : IFloxMenu
{
    /// <summary>Creates a menu.</summary>
    /// <param name="text">The title of the menu.</param>
    public FloxMenu(string? text = null)
    {
        Text = text;
    }

    /// <summary>The title of the menu.</summary>
    public string? Text { get; set; }
    /// <summary>The items of the menu, in the order they are shown.</summary>
    public FloxMenuItemList Items { get; set; } = [];
    /// <summary>The place of the menu among the menus of other modules; smaller is first.</summary>
    public int Position { get; set; }
}
