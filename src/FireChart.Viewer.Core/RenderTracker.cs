namespace FireChart.Viewer.Core;

// What the templates did with the elements of the resource that is rendered with the safety net: which they read and which were applied to a template.
// An element that was read and was not given to the generic template of a structure counts as shown; see SafetyNet
internal sealed class RenderTracker
{
    public const string Key = "fhir:tracker";

    private readonly HashSet<string> _touched = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rendered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _fallback = new(StringComparer.Ordinal);

    public void Touch(string location)
    {
        _touched.Add(location);
    }

    // fallback: the element went to _complex, which shows nothing of it
    public void Applied(string location, bool fallback)
    {
        (fallback ? _fallback : _rendered).Add(location);
    }

    public bool IsShown(string location)
    {
        return _touched.Contains(location) && (_rendered.Contains(location) || !_fallback.Contains(location));
    }
}
