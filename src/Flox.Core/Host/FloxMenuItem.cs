namespace Flox.Core.Host;

/// <summary>An entry of a menu: a link, or a group of links.</summary>
public class FloxMenuItem : IFloxMenuItem
{
    /// <summary>The icon (SVG markup) of an item that has none: an empty square.</summary>
    public const string NullIcon = "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g>";

    /// <summary>Creates a menu item.</summary>
    /// <param name="text">The text of the item.</param>
    /// <param name="icon">The SVG markup of the icon; an empty one is used when it is null.</param>
    /// <param name="href">The address the item leads to.</param>
    public FloxMenuItem(string? text = null, string? icon = null, string? href = null)
    {
        Text = text;
        Icon = icon ?? NullIcon;
        Href = href;
    }

    /// <summary>The icon as SVG markup.</summary>
    public string? Icon { get; set; }
    /// <summary>The text of the item.</summary>
    public string? Text { get; set; }
    /// <summary>The authorization policy the user must satisfy to see the item; none when null.</summary>
    public string? Policy { get; set; }
    /// <summary>The address the item leads to.</summary>
    public string? Href { get; set; }
    /// <summary>Where the shell shows a top-level item.</summary>
    public FloxMenuPlacement Placement { get; set; }
    /// <summary>The nested items.</summary>
    public FloxMenuItemList Items { get; set; } = [];
}
