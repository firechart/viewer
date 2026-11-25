using FireChart.Viewer.Components;
using FireChart.Viewer.Core.Reports;
using FireChart.Viewer.Routing;
using Flox.Shared.Components;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Reports;

// The row of a list: what leads it (an icon, a picture), the title and the second line, what ends it (a status, a badge); laid out as the rows of the
// templates (fx-tile)
internal sealed class TileBlock : ReportBlock
{
    private static readonly ReportValue s_record = ReportXml.ParseValue("{{%record}}");

    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private List<string> _parts = [];
    private string _code = string.Empty;

    // The record of the tile is read only for the options of a section (the age at it, its code)
    protected override IEnumerable<ReportValue?> Reads => Scope.Options.ShowAge || Scope.Options.CodeSystems is { Count: > 0 }
        ? [Node.Attribute("title"), Node.Attribute("subtitle"), Node.Attribute("patient"), Node.Attribute("encounter"), s_record]
        : [Node.Attribute("title"), Node.Attribute("subtitle"), Node.Attribute("patient"), Node.Attribute("encounter")];

    // The second line: its own facts, then the patient and the encounter of the record (patient, encounter: references, as slot_data of the
    // templates), each left out when it is the record the report is about
    protected override async Task PrepareAsync()
    {
        _title = Text("title");
        var parts = new List<string> { Text("subtitle") };
        foreach (var name in new[] { "patient", "encounter" })
        {
            if (Node.Attribute(name) is { } value && Values.Nodes(value) is [var reference, ..] && !Values.IsContext(reference))
            {
                parts.Add(ReportText.Children(reference, "display").Any()
                    ? Values.TextOf(reference)
                    : await Scope.Runtime.Data.ResolveAsync(reference) is { } record ? ReportText.TitleOf(record, Scope.Runtime.Format)
                    : Scope.Runtime.Data.MissingTypeOf(reference) is { } missing ? ReportBlocks.NotFound(missing)
                    : string.Empty);
            }
        }

        var own = Scope.Options.ShowAge || Scope.Options.CodeSystems is { Count: > 0 } ? Values.Nodes(s_record) is [var shown, ..] ? shown : null : null;
        if (Scope.Options.ShowAge && own is not null)
        {
            parts.Add(await AgeAtAsync(own));
        }

        _code = own is not null ? CodeOf(own, Scope.Options.CodeSystems ?? []) : string.Empty;
        _parts = [.. parts.Where(part => part.Length > 0)];
        _subtitle = string.Join(" · ", _parts);
    }

    // The age of the patient at the moment of the record: the patient of the tile (its attribute patient), else the patient the summary is about
    private async Task<string> AgeAtAsync(PocoNode record)
    {
        PocoNode? patient = null;
        if (Node.Attribute("patient") is { } value && Values.Nodes(value) is [var reference, ..])
        {
            patient = Values.IsContext(reference) ? Values.Context : await Scope.Runtime.Data.ResolveAsync(reference);
        }

        patient ??= Values.Context is { Poco.TypeName: "Patient" } context ? context : null;
        var format = Scope.Runtime.Format;
        var birth = patient is null ? null : ReportText.Children(patient, "birthDate").FirstOrDefault()?.Poco is PrimitiveType { JsonValue: { } born } ? born.ToString() : null;
        return birth is not null && ReportText.MomentOf(record) is { } moment && ReportText.DayOfEvent(moment, format) is { } day && ReportText.AgeOf(birth, day) is { } age
            ? ReportBlocks.AgeText(age)
            : string.Empty;
    }

    // The code of the record (its element code) of the first of the systems that it has, in their order
    private static string CodeOf(PocoNode record, IReadOnlyList<string> systems)
    {
        var codings = ReportText.Children(record, "code").SelectMany(code => ReportText.Children(code, "coding")).ToList();
        foreach (var system in systems)
        {
            foreach (var coding in codings)
            {
                if (ReportText.Children(coding, "system").FirstOrDefault()?.Poco is PrimitiveType { JsonValue: { } given } && given.ToString() == system
                    && ReportText.Children(coding, "code").FirstOrDefault()?.Poco is PrimitiveType { JsonValue: { } code })
                {
                    return code.ToString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        var leading = Node.Children.Where(child => child.Name == ReportSchema.Leading).ToList();
        var trailing = Node.Children.Where(child => child.Name == ReportSchema.Trailing).ToList();
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "fx-list-row");
        if (leading.Count > 0)
        {
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "class", "fx-summary-icon");
            builder.AddContent(4, Scope.Render(leading));
            builder.CloseElement();
        }

        builder.OpenElement(5, "div");
        builder.AddAttribute(6, "class", "fx-list-col-grow fx-tile");
        builder.OpenElement(7, "div");
        builder.AddAttribute(8, "class", "fx-tile-main");
        builder.OpenElement(9, "div");
        builder.AddAttribute(10, "class", "fx-tile-title");
        if (_code.Length > 0)
        {
            builder.OpenElement(21, "span");
            builder.AddAttribute(22, "class", "fx-tile-code");
            builder.AddContent(23, _code);
            builder.CloseElement();
            builder.AddContent(24, " ");
        }

        ReportBlocks.Isolated(builder, _title, null);
        builder.CloseElement();
        var subtitle = Node.Children.Where(child => child.Name == ReportSchema.Subtitle).ToList();
        if (_subtitle.Length > 0 || subtitle.Count > 0)
        {
            // The part Subtitle first, then the subtitle of the attributes (its own text, the patient, the encounter)
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "class", "fx-tile-subtitle");
            builder.AddContent(19, Scope.Render(subtitle));
            if (subtitle.Count > 0 && _subtitle.Length > 0)
            {
                builder.AddContent(20, " · ");
            }

            // Each part apart: in a right-to-left page the parts follow its direction, each keeps the order of its own text
            for (var i = 0; i < _parts.Count; i++)
            {
                if (i > 0)
                {
                    builder.AddContent(25, " · ");
                }

                ReportBlocks.Isolated(builder, _parts[i], null);
            }
            builder.CloseElement();
        }

        builder.CloseElement();
        if (trailing.Count > 0)
        {
            builder.OpenElement(15, "div");
            builder.AddAttribute(16, "class", "fx-tile-end");
            builder.AddContent(17, Scope.Render(trailing));
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }
}

// What leads a row: an icon of the kit (icon="person"), or its inline content
internal sealed class LeadingBlock : ReportBlock
{
    private string _icon = string.Empty;

    protected override void Prepare()
    {
        _icon = Text("icon");
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        if (_icon.Length > 0)
        {
            builder.OpenComponent<FloxIcon>(0);
            builder.AddComponentParameter(1, nameof(FloxIcon.Icon), _icon);
            builder.CloseComponent();
        }

        builder.AddContent(2, Children);
    }
}

// The content of a part of a frame (Trailing of a Tile, Facts and Flags of a Banner): its blocks, side by side
internal sealed class PartBlock : ReportBlock
{
    protected override void Build(RenderTreeBuilder builder)
    {
        if (Node.Name != ReportSchema.Facts)
        {
            builder.AddContent(0, Children);
            return;
        }

        // The facts of a banner are one span each, as the kit spaces them
        foreach (var child in Node.Children)
        {
            builder.OpenElement(1, "span");
            builder.AddContent(2, Scope.Render([child]));
            builder.CloseElement();
        }
    }
}

// The head of a record: its title, the facts under it, the flags (deceased, inactive); as the banners of the templates (fx-banner)
internal sealed class BannerBlock : ReportBlock
{
    private string _title = string.Empty;

    protected override IEnumerable<ReportValue?> Reads => [Node.Attribute("title")];

    protected override void Prepare()
    {
        _title = Text("title");
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "fx-banner");
        builder.OpenElement(2, "h2");
        builder.AddAttribute(3, "class", "fx-banner-title");
        builder.AddContent(4, _title);
        builder.CloseElement();
        Part(builder, 5, "fx-banner-facts", ReportSchema.Facts);
        Part(builder, 10, "fx-banner-flags", ReportSchema.Flags);
        builder.CloseElement();
    }

    private void Part(RenderTreeBuilder builder, int sequence, string css, string name)
    {
        builder.OpenElement(sequence, "div");
        builder.AddAttribute(sequence + 1, "class", css);
        builder.AddContent(sequence + 2, Scope.Render(Node.Children.Where(child => child.Name == name)));
        builder.CloseElement();
    }
}

// An element in a mode, as `apply x mode:` of the templates: the component {Type}.{mode} of the library with the element as %record, else the
// built-in view of the mode (text, a link, a tile with the title of the record, a banner, the list of its elements). A tile of a record is a link to
// its page
internal sealed class ShowBlock : ReportBlock
{
    private List<Shown> _shown = [];
    private string _mode = "text";

    // Where a row of a tile leads, when the host decides (a section of a summary: a sheet over the page, the summary of the record); the page of the
    // record otherwise
    [Parameter]
    public Func<PocoNode, string?>? HrefOf { get; set; }

    private Func<PocoNode, string?>? _hrefOf;

    // A delegate of the same method of the same host is equal: the rows of a section drawn again lead where they led
    protected override bool SameParameters()
    {
        var same = Equals(_hrefOf, HrefOf);
        _hrefOf = HrefOf;
        return same;
    }

    // The rows are made apart and put in place whole: a block given new parameters while it still waits prepares again beside the first run
    protected override async Task PrepareAsync()
    {
        var mode = Text("mode");
        var shown = new List<Shown>();
        foreach (var given in Values.Nodes(Node.Attribute("value")!))
        {
            // A reference in a tile, a banner or a view is the record it points to, when it can be read
            var node = given.Poco.TypeName == "Reference" && mode is "tile" or "banner" or "view"
                ? await Scope.Runtime.Data.ResolveAsync(given) ?? given
                : given;
            if (node == given && given.Poco.TypeName == "Reference" && Scope.Runtime.Data.MissingTypeOf(given) is { } missing)
            {
                shown.Add(new Shown(given, null, missing));
                continue;
            }

            var component = mode is "text" ? null : await Scope.Runtime.ModeAsync(node.Poco.TypeName, mode);
            shown.Add(new Shown(node, component));
        }

        _mode = mode;
        _shown = shown;
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        foreach (var shown in _shown)
        {
            builder.OpenRegion(0);
            if (shown.Missing is { } type)
            {
                ReportBlocks.Badge(builder, ReportBlocks.NotFound(type), "warning");
            }
            else if (_mode == "tile")
            {
                Row(builder, shown);
            }
            else
            {
                One(builder, shown);
            }

            builder.CloseRegion();
        }
    }

    private void One(RenderTreeBuilder builder, Shown shown)
    {
        if (shown.Component is { } component)
        {
            builder.OpenComponent<ModeHost>(0);
            builder.AddComponentParameter(1, nameof(ModeHost.Component), component);
            builder.AddComponentParameter(2, nameof(ModeHost.Record), shown.Node);
            builder.AddComponentParameter(3, nameof(ModeHost.Scope), Scope);
            builder.CloseComponent();
            return;
        }

        var culture = Scope.Runtime.Format;
        switch (_mode)
        {
            case "link":
                builder.OpenComponent<LinkBlock>(4);
                builder.AddComponentParameter(5, nameof(ReportBlock.Node), LinkNode);
                builder.AddComponentParameter(6, nameof(ReportBlock.Scope), Scope.With(ReportVariables.Record, [shown.Node]));
                builder.CloseComponent();
                break;
            case "tile":
                Tile(builder, shown.Node, culture);
                break;
            case "banner":
                builder.OpenElement(7, "div");
                builder.AddAttribute(8, "class", "fx-banner fx-banner-sub");
                builder.OpenElement(9, "h2");
                builder.AddAttribute(10, "class", "fx-banner-title");
                builder.AddContent(11, ReportText.TitleOf(shown.Node, culture));
                builder.CloseElement();
                builder.CloseElement();
                break;
            case "view":
                builder.OpenElement(12, "section");
                builder.AddAttribute(13, "class", "fx-section");
                foreach (var field in ReportText.FieldsOf(shown.Node, culture))
                {
                    builder.OpenElement(14, "dl");
                    builder.AddAttribute(15, "class", "fx-field");
                    builder.OpenElement(16, "dt");
                    builder.AddAttribute(17, "class", "fx-label");
                    builder.AddContent(18, field.Label);
                    builder.CloseElement();
                    builder.OpenElement(19, "dd");
                    builder.AddAttribute(20, "class", "fx-value");
                    builder.AddContent(21, field.Text);
                    builder.CloseElement();
                    builder.CloseElement();
                }

                builder.CloseElement();
                break;
            default:
                builder.AddContent(22, ReportText.Of(shown.Node, culture));
                break;
        }
    }

    // The built-in tile: the title of the record, when it happened, its status
    private static void Tile(RenderTreeBuilder builder, PocoNode record, ReportFormat culture)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "fx-list-row");
        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "fx-list-col-grow fx-tile");
        builder.OpenElement(4, "div");
        builder.AddAttribute(5, "class", "fx-tile-main");
        builder.OpenElement(6, "div");
        builder.AddAttribute(7, "class", "fx-tile-title");
        builder.AddContent(8, ReportText.TitleOf(record, culture));
        builder.CloseElement();
        if (ReportText.WhenOf(record, culture) is { Length: > 0 } when)
        {
            builder.OpenElement(9, "div");
            builder.AddAttribute(10, "class", "fx-tile-subtitle");
            builder.AddContent(11, when);
            builder.CloseElement();
        }

        builder.CloseElement();
        if (ReportText.Children(record, "status").FirstOrDefault() is { } status)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "class", "fx-tile-end");
            ReportBlocks.Badge(builder, ReportText.Of(status, culture), ReportTones.Status(ReportTones.CodeOf(status)));
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    // A row of a list: the icon of the type unless the tile leads with its own, the tile, a link to the page of the record
    private void Row(RenderTreeBuilder builder, Shown shown)
    {
        var href = HrefOf is not null
            ? HrefOf(shown.Node)
            : Session is not null && shown.Node.Poco is Resource { Id: { } id } resource
                ? Session.HrefOf(new ViewerRoute(ViewerPage.Resource, resource.TypeName, id))
                : null;
        builder.OpenElement(0, href is null ? "div" : "a");
        builder.AddAttribute(1, "class", "fx-summary-row");
        if (href is not null)
        {
            builder.AddAttribute(2, "href", href);
        }

        if (shown.Component?.Body.FirstOrDefault(node => node.Name == ReportSchema.Tile)?.Children.Any(child => child.Name == ReportSchema.Leading) != true)
        {
            builder.OpenElement(3, "div");
            builder.AddAttribute(4, "class", "fx-summary-icon");
            builder.OpenComponent<IconResource>(5);
            builder.AddComponentParameter(6, nameof(IconResource.ResourceType), shown.Node.Poco.TypeName);
            builder.CloseComponent();
            builder.CloseElement();
        }

        builder.OpenElement(7, "div");
        builder.AddAttribute(8, "class", "fx-list fx-summary-tile");
        One(builder, shown);
        builder.CloseElement();
        builder.CloseElement();
    }

    private static readonly ReportNode LinkNode = ReportXml.Parse("""<Component name="Link"><Link value="{{%record}}"/></Component>""").Body[0];

    private sealed record Shown(PocoNode Node, ComponentDefinition? Component, string? Missing = null);
}

// The body of a component of a mode with the element as %record: a scope of its own, as a component called by name
internal sealed class ModeHost : ComponentBase
{
    [Parameter]
    public ComponentDefinition Component { get; set; } = default!;

    [Parameter]
    public PocoNode Record { get; set; } = default!;

    [Parameter]
    public ReportScope Scope { get; set; } = default!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Scope.Depth >= ReportRuntime.MaxDepth)
        {
            ReportRenderer.Note(builder, $"{Component.Name}: components nested deeper than {ReportRuntime.MaxDepth}");
            return;
        }

        var inner = Scope.ForComponent([KeyValuePair.Create(ReportVariables.Record, Task.FromResult<IReadOnlyList<PocoNode>>([Record]))],
                                         System.Collections.Immutable.ImmutableDictionary<string, System.Collections.Immutable.ImmutableArray<ReportNode>>.Empty)
                         .WithSearches(Component.Data);
        builder.AddContent(0, inner.Render(Component.Body));
    }
}
