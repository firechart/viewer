namespace Flox.Core.Host;

public interface IFloxMenuItem
{
    string? Href { get; set; }
    string? Icon { get; set; }
    FloxMenuItemList Items { get; }
    string? Text { get; set; }

    // Optional authorization policy the current user must satisfy for the item to be shown
    string? Policy { get; set; }

    // Where the shell shows a top-level item: in the navigation menu, or on the More page
    FloxMenuPlacement Placement { get; set; }
}

public enum FloxMenuPlacement
{
    Main = 0,

    // Rarely used pages and the ones that belong to no category; the navigation menu has one entry, More, for all of them
    More = 1
}
