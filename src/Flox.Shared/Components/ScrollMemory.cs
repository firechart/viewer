namespace Flox.Shared.Components;

/// <summary>
/// Where a page was scrolled to. It lives in the state of the page, so it outlives the widget: a scrolling component writes it while the user scrolls and reads it when the page comes back (the same key of the state), and a new key of the state starts at the top.
/// </summary>
public sealed class ScrollMemory
{
    /// <summary>
    /// The scroll offset from the top, in pixels.
    /// </summary>
    public double Top { get; set; }
}
