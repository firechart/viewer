using Fluid;

namespace FireChart.Viewer.Core;

// What a template of a frame filled (a tile, a banner): the HTML of its slots ({% slot %}) and, for a tile, of the context line ({% slot_data %}: the
// patient, the encounter, the age). Present only while a frame is rendered: elsewhere the tags write and keep nothing
internal sealed class FrameSlots
{
    public const string Key = "fhir:slots";

    public const string Title = "title";
    public const string Subtitle = "subtitle";
    public const string Trailing = "trailing";
    public const string Status = "status";
    public const string Patient = "patient";
    public const string Encounter = "encounter";
    public const string Age = "age";
    public const string At = "at";
    public const string Facts = "facts";
    public const string Flags = "flags";

    public Dictionary<string, string> Html { get; } = new(StringComparer.Ordinal);

    public bool IsEmpty => Html.Count == 0;

    public static FrameSlots? Of(TemplateContext context)
    {
        return context.AmbientValues.TryGetValue(Key, out var value) ? value as FrameSlots : null;
    }

    public string this[string name]
    {
        get
        {
            return Html.TryGetValue(name, out var html) ? html : string.Empty;
        }
    }
}
