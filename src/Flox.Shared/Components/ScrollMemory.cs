namespace Flox.Shared.Components;

// Where a page was scrolled to. It lives in the State of the page, so it outlives the widget: a scrolling component writes it while the user scrolls
// and reads it when the page comes back (the same key of the state), and a new key of the state starts at the top
public sealed class ScrollMemory
{
    public double Top { get; set; }
}
