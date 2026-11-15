namespace Flox.Core.Host;

/// <summary>A list of menu items with a fluent way to add a plain item.</summary>
public class FloxMenuItemList : List<IFloxMenuItem>
{
    /// <summary>Adds a plain item and returns the list, so calls can be chained.</summary>
    /// <param name="text">The text of the item.</param>
    /// <param name="icon">The SVG markup of the icon.</param>
    /// <param name="href">The address the item leads to.</param>
    /// <returns>This list.</returns>
    public FloxMenuItemList Add(string? text = null, string? icon = null, string? href = null)
    {
        Add(new FloxMenuItem(text, icon, href));
        return this;
    }
}
