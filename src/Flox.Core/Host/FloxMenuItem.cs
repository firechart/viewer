namespace Flox.Core.Host;

public class FloxMenuItem : IFloxMenuItem
{
    public const string NullIcon = "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g>";

    public FloxMenuItem(string? text = null, string? icon = null, string? href = null)
    {
        Text = text;
        Icon = icon ?? NullIcon;
        Href = href;
    }

    public string? Icon { get; set; }
    public string? Text { get; set; }
    public string? Policy { get; set; }
    public string? Href { get; set; }
    public FloxMenuPlacement Placement { get; set; }
    public FloxMenuItemList Items { get; set; } = [];
}
