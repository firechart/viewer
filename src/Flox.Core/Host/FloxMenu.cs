namespace Flox.Core.Host;

public class FloxMenu : IFloxMenu
{
    public FloxMenu(string? text = null)
    {
        Text = text;
    }

    public string? Text { get; set; }
    public FloxMenuItemList Items { get; set; } = [];
    public int Position { get; set; }
}
