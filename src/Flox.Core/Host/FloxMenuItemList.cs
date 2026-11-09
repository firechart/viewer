namespace Flox.Core.Host;

public class FloxMenuItemList : List<IFloxMenuItem>
{
    public FloxMenuItemList Add(string? text = null, string? icon = null, string? href = null)
    {
        Add(new FloxMenuItem(text, icon, href));
        return this;
    }
}
