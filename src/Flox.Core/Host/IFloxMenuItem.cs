namespace Flox.Core.Host;

/// <summary>An entry of a menu: a link, or a group of links.</summary>
public interface IFloxMenuItem
{
    /// <summary>The address the item leads to.</summary>
    string? Href { get; set; }
    /// <summary>The icon as SVG markup.</summary>
    string? Icon { get; set; }
    /// <summary>The nested items.</summary>
    FloxMenuItemList Items { get; }
    /// <summary>The text of the item.</summary>
    string? Text { get; set; }

    /// <summary>The authorization policy the user must satisfy to see the item; none when null.</summary>
    string? Policy { get; set; }

    /// <summary>Where the shell shows a top-level item.</summary>
    FloxMenuPlacement Placement { get; set; }
}

/// <summary>Where the shell shows a top-level menu item.</summary>
public enum FloxMenuPlacement
{
    /// <summary>In the navigation menu.</summary>
    Main = 0,

    /// <summary>On the More page: rarely used pages and the ones that belong to no category; the navigation menu has one entry, More, for all of them.</summary>
    More = 1
}
