using System.Collections.Immutable;
using FireChart.Viewer.Core.Reports;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Components;
using Task = System.Threading.Tasks.Task;
using Microsoft.AspNetCore.Components.Rendering;

namespace FireChart.Viewer.Reports;

// A built-in block: it waits for the variables of the values it reads (a search answers late), shows a skeleton meanwhile and a note when a search
// or an expression fails; the rest of the report goes on. Prepare computes what Build shows, so a failure never leaves half a render tree
internal abstract class ReportBlock : ComponentBase
{
    private bool _ready;
    private string? _failure;
    private ReportValues? _values;
    private Task? _work;
    private ReportNode? _node;
    private ReportScope? _scope;
    private int _generation;
    private bool _unchanged;

    [Parameter]
    public ReportNode Node { get; set; } = default!;

    [Parameter]
    public ReportScope Scope { get; set; } = default!;

    // The Viewer around the report: the links to records; none in a test
    [CascadingParameter]
    public ViewerSession? Session { get; set; }

    // The values this block reads: by default its attributes
    protected virtual IEnumerable<ReportValue?> Reads => Node.Attributes.Values;

    // The values read, answered (set before Prepare)
    protected ReportValues Values => _values!;

    protected string Text(string attribute)
    {
        return Values.Text(Node.Attribute(attribute));
    }

    protected RenderFragment Children => Scope.Render(Node.Children);

    // The values of the block and of all its content: a block that decides by its content whether it shows anything waits for them all
    protected IEnumerable<ReportValue?> DeepReads => Descendants(Node).SelectMany(node => node.Attributes.Values);

    private static IEnumerable<ReportNode> Descendants(ReportNode node)
    {
        return node.Children.SelectMany(Descendants).Prepend(node);
    }

    // Whether an inline block shows nothing (its values read: DeepReads); a literal text, or a block it cannot tell, shows something
    protected bool IsEmpty(ReportNode node)
    {
        return node.Name switch
        {
            ReportNode.TextName => Values.Text(node.Text).Trim().Length == 0,
            ReportSchema.Value => Values.Nodes(node.Attribute("value")!).Count == 0 && Values.Text(node.Attribute("empty")).Length == 0,
            ReportSchema.Link or ReportSchema.Status or ReportSchema.Interpretation or ReportSchema.Show => Values.Nodes(node.Attribute("value")!).Count == 0,
            ReportSchema.Badge => Values.Text(node.Attribute("text")).Length == 0 && node.Children.All(IsEmpty),
            ReportSchema.If => !Values.IsTrue(node.Attribute("test")!) || node.Children.All(IsEmpty),
            ReportSchema.Text or ReportSchema.Parts => node.Children.All(IsEmpty),
            ReportSchema.Age => AgeBlock.TextOf(node, Values, Scope.Runtime.Format).Length == 0,
            _ => false
        };
    }

    // Whether the parameters of a block of its own (beyond the node and the scope) are those of its last load
    protected virtual bool SameParameters()
    {
        return true;
    }

    protected override bool ShouldRender()
    {
        var render = !_unchanged;
        _unchanged = false;
        return render;
    }

    protected override async Task OnParametersSetAsync()
    {
        // A render of the page sets the same node and scope again: a block still waiting for its data keeps waiting, and is not counted again (a
        // report loaded at once draws again at every step of its progress, which would start the block over and over); a block that has its values
        // neither reads them again nor draws again (a section of a few hundred records draws again at every page it loads) until the words of the
        // codes come
        var generation = Scope.Runtime.Generation;
        var same = SameParameters();
        if (_work is not null && ReferenceEquals(_node, Node) && ReferenceEquals(_scope, Scope) && same && (!_work.IsCompleted || _generation == generation))
        {
            _unchanged = true;
            return;
        }

        _node = Node;
        _scope = Scope;
        _generation = generation;
        _failure = null;
        var work = LoadAsync();
        _work = work;
        _ready = work.IsCompleted;

        // A block that waits (for a search, for a component of a mode) is counted, so a report loaded at once knows when everything has come
        var progress = _ready ? null : Scope.Runtime.Data.Progress;
        progress?.Begin();
        try
        {
            await work;
        }
        catch (OperationCanceledException)
        {
            _failure = "not loaded: the loading was cancelled";
        }
        catch (Exception ex)
        {
            _failure = ex.Message;
        }
        finally
        {
            progress?.End();
        }

        _ready = true;
    }

    private async Task LoadAsync()
    {
        _values = await Scope.ResolveAsync(Reads);
        await PrepareAsync();
    }

    // What Build shows, computed when the values have come; a block that needs more (a component of a mode) awaits it here
    protected virtual Task PrepareAsync()
    {
        Prepare();
        return Task.CompletedTask;
    }

    protected virtual void Prepare()
    {
    }

    protected abstract void Build(RenderTreeBuilder builder);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_failure is not null)
        {
            ReportRenderer.Note(builder, $"{Node.Name} (line {Node.Line}): {_failure}");
        }
        else if (!_ready)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "fx-skeleton");
            builder.AddAttribute(2, "data-report-loading", true);
            builder.AddAttribute(3, "style", "display:block;min-height:1.5rem;border-radius:.25rem;background:var(--fx-border, #8883)");
            builder.CloseElement();
        }
        else
        {
            Build(builder);
        }
    }
}

internal sealed class TextBlock : ReportBlock
{
    private string _text = string.Empty;

    protected override IEnumerable<ReportValue?> Reads => [Node.Text];

    protected override void Prepare()
    {
        _text = Values.Text(Node.Text);
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.AddContent(0, _text);
    }
}

internal sealed class SectionBlock : ReportBlock
{
    private string _title = string.Empty;
    private string _count = string.Empty;

    protected override void Prepare()
    {
        _title = Text("title");
        _count = Text("count");
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "section");
        builder.AddAttribute(1, "class", "fx-section");
        if (_title.Length > 0)
        {
            builder.OpenElement(2, "h3");
            builder.AddAttribute(3, "class", "fx-section-title");
            builder.AddContent(4, _title);
            if (_count.Length > 0)
            {
                builder.OpenElement(10, "span");
                builder.AddAttribute(11, "class", "fx-count");
                builder.AddContent(12, _count);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.AddContent(5, Children);
        builder.CloseElement();
    }
}

internal sealed class CardBlock : ReportBlock
{
    private string _title = string.Empty;

    protected override void Prepare()
    {
        _title = Text("title");
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "fx-card fx-card-border fx-card-sm");
        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "fx-card-body");
        if (_title.Length > 0)
        {
            builder.OpenElement(4, "h4");
            builder.AddAttribute(5, "class", "fx-card-title");
            builder.AddContent(6, _title);
            builder.CloseElement();
        }

        builder.AddContent(7, Children);
        builder.CloseElement();
        builder.CloseElement();
    }
}

// Side by side while they fit, one under the other on a narrow screen
internal sealed class ColumnsBlock : ReportBlock
{
    protected override void Build(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "style", "display:grid;grid-template-columns:repeat(auto-fit,minmax(16rem,1fr));gap:var(--fx-space-3, .75rem)");
        builder.AddContent(2, Children);
        builder.CloseElement();
    }
}

// A label and its value: the value is "value" as text, or the content; a field whose "value" is empty and that has no content is not shown
internal sealed class FieldBlock : ReportBlock
{
    private string _label = string.Empty;
    private string _value = string.Empty;
    private bool _empty;

    // Its own values and those of its content: a field whose value and content would show nothing is not shown (no label alone)
    protected override IEnumerable<ReportValue?> Reads => DeepReads;

    protected override void Prepare()
    {
        _label = Text("label");
        _value = Text("value");
        _empty = _value.Length == 0 && Node.Children.All(IsEmpty);
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        if (_empty)
        {
            return;
        }

        builder.OpenElement(0, "dl");
        builder.AddAttribute(1, "class", "fx-field");
        builder.OpenElement(2, "dt");
        builder.AddAttribute(3, "class", "fx-label");
        builder.AddContent(4, _label);
        builder.CloseElement();
        builder.OpenElement(5, "dd");
        builder.AddAttribute(6, "class", "fx-value");
        if (!Node.Children.IsEmpty)
        {
            // The inline blocks of a value (a value, its badges) side by side with a gap, wrapping on a narrow screen
            builder.AddAttribute(7, "style", "display:flex;flex-wrap:wrap;align-items:center;gap:.25rem .5rem");
        }

        builder.AddContent(8, _value);
        builder.AddContent(9, Children);
        builder.CloseElement();
        builder.CloseElement();
    }
}

internal sealed class ValueBlock : ReportBlock
{
    private string _text = string.Empty;
    private string? _direction;

    protected override void Prepare()
    {
        var of = Node.Attribute("value");
        var text = Node.Attribute("separator") is { } separator && of is not null ? Values.Join(of, Values.Text(separator)) : Text("value");
        _text = text.Length > 0 ? text : Text("empty");
        _direction = text.Length > 0 && of is not null ? ReportBlocks.DirectionOf(Values.Nodes(of)) : null;
    }

    // A value is isolated from the text around it (bdi): in a right-to-left page a quantity, a date or a phrase of the record keeps its order
    protected override void Build(RenderTreeBuilder builder)
    {
        ReportBlocks.Isolated(builder, _text, _direction);
    }
}

// An age from a birth date: today in the zone of the display, or at the moment of an event (at: a date, a moment, a Period); nothing when the event
// has no date (deceased: true) or the birth date cannot be read
internal sealed class AgeBlock : ReportBlock
{
    private string _text = string.Empty;

    protected override void Prepare()
    {
        _text = TextOf(Node, Values, Scope.Runtime.Format);
    }

    // The text of an Age, its values read; empty when it shows nothing (a Parts leaves out its separator then)
    public static string TextOf(ReportNode node, ReportValues values, ReportFormat format)
    {
        var birth = values.Nodes(node.Attribute("birth")!) is [{ Poco: PrimitiveType { JsonValue: { } value } }, ..] ? value.ToString() ?? string.Empty : string.Empty;
        DateOnly? day = format.Today;
        if (node.Attribute("at") is { } at && values.Nodes(at) is [var when, ..])
        {
            // deceased: false is alive (today), true has no date of death (no age)
            day = when.Poco is PrimitiveType { TypeName: "boolean", JsonValue: bool dead } ? dead ? null : format.Today : ReportText.DayOfEvent(when, format);
        }

        return day is { } until && ReportText.AgeOf(birth, until) is { } age ? ReportBlocks.AgeText(age) : string.Empty;
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.AddContent(0, _text);
    }
}

internal sealed class BadgeBlock : ReportBlock
{
    private string _text = string.Empty;
    private string _tone = string.Empty;

    protected override void Prepare()
    {
        _text = Text("text");
        _tone = ReportBlocks.BadgeTone(Text("tone"));
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        if (_text.Length == 0 && Node.Children.IsEmpty)
        {
            return;
        }

        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", $"fx-badge fx-badge-soft fx-badge-sm {_tone}".TrimEnd());
        builder.AddContent(2, _text);
        builder.AddContent(3, Children);
        builder.CloseElement();
    }
}

internal sealed class SpanBlock : ReportBlock
{
    protected override void Build(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddContent(1, Children);
        builder.CloseElement();
    }
}

// Its content once per element of "each", each with the element as %item (or the name of "as"); "empty" when there is none
internal sealed class RepeatBlock : ReportBlock
{
    private IReadOnlyList<PocoNode> _items = [];
    private string _name = ReportVariables.Item;

    protected override IEnumerable<ReportValue?> Reads => [Node.Attribute("each"), Node.Attribute("empty")];

    protected override void Prepare()
    {
        _items = Values.Nodes(Node.Attribute("each")!);
        _name = Node.Attribute("as")?.Source is { Length: > 0 } name ? name : ReportVariables.Item;
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        if (_items.Count == 0)
        {
            if (Text("empty") is { Length: > 0 } empty)
            {
                builder.OpenElement(0, "p");
                builder.AddAttribute(1, "class", "fx-empty");
                builder.AddContent(2, empty);
                builder.CloseElement();
            }

            return;
        }

        foreach (var item in _items)
        {
            builder.AddContent(3, Scope.With(_name, [item]).Render(Node.Children));
        }
    }
}

internal sealed class IfBlock : ReportBlock
{
    private bool _true;

    protected override void Prepare()
    {
        _true = Values.IsTrue(Node.Attribute("test")!);
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        if (_true)
        {
            builder.AddContent(0, Children);
        }
    }
}

// The content the caller gave to a slot, rendered in the scope of the caller
internal sealed class SlotOutletBlock : ReportBlock
{
    protected override IEnumerable<ReportValue?> Reads => [];

    protected override void Build(RenderTreeBuilder builder)
    {
        var name = Node.Attribute("name")?.Source ?? string.Empty;
        if (Scope.SlotOwner is { } owner && Scope.Slots.TryGetValue(name, out var content))
        {
            builder.AddContent(0, owner.Render(content));
        }
    }
}

// A component of the project: its parameters are evaluated where it is called, its body sees only them and the context, its searches start now
internal sealed class CompositeBlock : ReportBlock
{
    private ComponentDefinition? _definition;
    private ReportScope? _inner;

    // The parameters are passed as tasks: the component does not wait for the searches of its caller, its own blocks do
    protected override IEnumerable<ReportValue?> Reads => [];

    protected override void Prepare()
    {
        if (Scope.Depth >= ReportRuntime.MaxDepth)
        {
            throw new InvalidOperationException($"components nested deeper than {ReportRuntime.MaxDepth}");
        }

        _definition = Scope.Runtime.Components[Node.Name];
        var parameters = _definition.Params.ToDictionary(param => param.Name,
                                                         param => Node.Attribute(param.Name) is { } value ? Scope.ArgumentAsync(value) : Task.FromResult<IReadOnlyList<PocoNode>>([]));
        var slots = Node.Children.Where(child => child.Name == ReportSchema.Fill)
                        .ToImmutableDictionary(child => child.Attribute("slot")?.Source ?? string.Empty, child => child.Children);
        var content = Node.Children.Where(child => child.Name != ReportSchema.Fill).ToImmutableArray();
        if (!content.IsEmpty)
        {
            slots = slots.SetItem(string.Empty, content);
        }

        _inner = Scope.ForComponent(parameters, slots).WithSearches(_definition.Data);
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.AddContent(0, _inner!.Render(_definition!.Body));
    }
}

// Checked away before a report is shown; kept so a node never breaks the render
internal sealed class UnknownBlock : ReportBlock
{
    protected override IEnumerable<ReportValue?> Reads => [];

    protected override void Build(RenderTreeBuilder builder)
    {
        ReportRenderer.Note(builder, $"<{Node.Name}> (line {Node.Line}) is not a component");
    }
}

internal static class ReportBlocks
{
    public static string BadgeTone(string tone)
    {
        return tone switch
        {
            "danger" => "fx-badge-error",
            "warning" => "fx-badge-warning",
            "success" => "fx-badge-success",
            "info" => "fx-badge-info",
            _ => string.Empty
        };
    }

    // "49 y", "7 mo", "12 d" in the language of the application
    public static string AgeText(ReportAge age)
    {
        var pattern = age.Unit switch
        {
            ReportAgeUnit.Years => Strings.Caption_AgeYears,
            ReportAgeUnit.Months => Strings.Caption_AgeMonths,
            _ => Strings.Caption_AgeDays
        };
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, pattern, age.Count);
    }

    // The text of a reference whose record the server says does not exist ("Patient could not be found")
    public static string NotFound(string type)
    {
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Message_RecordNotFound, type);
    }

    // The direction of values whose text reads left to right whatever the language (a number, a date, a quantity, an identifier); null: as their
    // own letters say (bdi without dir)
    public static string? DirectionOf(IReadOnlyList<PocoNode> nodes)
    {
        return nodes.Count > 0 && nodes.All(node => s_leftToRight.Contains(node.Poco.TypeName)) ? "ltr" : null;
    }

    private static readonly HashSet<string> s_leftToRight = new(StringComparer.Ordinal)
    {
        "date", "dateTime", "instant", "time", "decimal", "integer", "integer64", "positiveInt", "unsignedInt", "id", "uri", "url", "oid", "uuid",
        "Quantity", "SimpleQuantity", "Age", "Duration", "Distance", "Count", "MoneyQuantity", "Money", "Range", "Ratio", "Period", "Identifier",
        "ContactPoint", "SampledData"
    };

    // A text isolated from the text around it, in a direction of its own (ltr) or as its letters say
    public static void Isolated(RenderTreeBuilder builder, string text, string? direction)
    {
        if (text.Length == 0)
        {
            return;
        }

        builder.OpenElement(0, "bdi");
        if (direction is not null)
        {
            builder.AddAttribute(1, "dir", direction);
        }

        builder.AddContent(2, text);
        builder.CloseElement();
    }

    public static void Badge(RenderTreeBuilder builder, string text, string tone)
    {
        if (text.Length == 0)
        {
            return;
        }

        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", $"fx-badge fx-badge-soft fx-badge-sm {BadgeTone(tone)}".TrimEnd());
        builder.AddContent(2, text);
        builder.CloseElement();
    }
}

// A line of a list: what it is (main) and about it (meta, and the inline content)
internal sealed class RowBlock : ReportBlock
{
    private string _main = string.Empty;
    private string _meta = string.Empty;

    protected override void Prepare()
    {
        _main = Text("main");
        _meta = Text("meta");
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "fx-row");
        builder.OpenElement(2, "span");
        builder.AddAttribute(3, "class", "fx-row-main");
        builder.AddContent(4, _main);
        builder.AddContent(18, Scope.Render(Node.Children.Where(child => child.Name == ReportSchema.Main)));
        builder.CloseElement();
        if (_meta.Length > 0)
        {
            builder.OpenElement(5, "span");
            builder.AddAttribute(6, "class", "fx-row-meta");
            builder.AddContent(7, _meta);
            builder.CloseElement();
        }

        var meta = Node.Children.Where(child => child.Name != ReportSchema.Main).ToList();
        if (meta.Count > 0)
        {
            builder.OpenElement(8, "span");
            builder.AddAttribute(9, "class", "fx-row-meta");
            builder.AddContent(10, Scope.Render(meta));
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}

// A status (or an interpretation) as a badge, its tone by the code (completed is success, cancelled danger); the label is the display unless given
internal sealed class StatusBlock : ReportBlock
{
    private string _text = string.Empty;
    private string _tone = string.Empty;

    protected override void Prepare()
    {
        var nodes = Values.Nodes(Node.Attribute("value")!);
        var node = nodes.Count > 0 ? nodes[0] : null;
        var code = node is null ? null : ReportTones.CodeOf(node);
        var shown = node is null ? string.Empty : Values.TextOf(node);
        _text = Text("label") is { Length: > 0 } label ? label : shown.Length > 0 ? shown : code ?? string.Empty;
        _tone = Node.Name == ReportSchema.Interpretation ? ReportTones.Interpretation(code) : ReportTones.Status(code);
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        // A section may leave the statuses of its tiles out (Feed status="false")
        if (Node.Name == ReportSchema.Status && !Scope.Options.ShowStatus)
        {
            return;
        }

        ReportBlocks.Badge(builder, _text, _tone);
    }
}

// A reference (or a resource) as a link to its page in the Viewer; its display, else the reference. A reference to another server is text
internal sealed partial class LinkBlock : ReportBlock
{
    private readonly List<ReportLink> _links = [];

    protected override async Task PrepareAsync()
    {
        _links.Clear();
        var given = Text("text");
        foreach (var node in Values.Nodes(Node.Attribute("value")!))
        {
            var target = Target(node);
            var text = given.Length > 0 ? given : await TextOfAsync(node);
            var missing = node.Poco.TypeName == "Reference" ? Scope.Runtime.Data.MissingTypeOf(node) : null;
            var href = Session is not null && target is not null && missing is null ? Session.HrefOf(target) : null;
            _links.Add(new ReportLink(text, href, missing));
        }
    }

    // A reference reads as its display; without one, as the title of the record it points to, else as the reference itself
    private async Task<string> TextOfAsync(PocoNode node)
    {
        if (node.Poco.TypeName != "Reference" || ReportText.Children(node, "display").Any())
        {
            return Values.TextOf(node);
        }

        return await Scope.Runtime.Data.ResolveAsync(node) is { } record ? ReportText.TitleOf(record, Scope.Runtime.Format) : Values.TextOf(node);
    }

    private static Routing.ViewerRoute? Target(PocoNode node)
    {
        if (node.Poco is Resource { Id: { } id } resource)
        {
            return new Routing.ViewerRoute(Routing.ViewerPage.Resource, resource.TypeName, id);
        }

        var reference = ReportText.Children(node, "reference").FirstOrDefault()?.Poco is PrimitiveType value ? value.JsonValue?.ToString() : null;
        return reference is not null && RelativeReference().Match(reference) is { Success: true } match
            ? new Routing.ViewerRoute(Routing.ViewerPage.Resource, match.Groups[1].Value, match.Groups[2].Value)
            : null;
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        var first = true;
        foreach (var link in _links)
        {
            if (!first)
            {
                builder.AddContent(0, ", ");
            }

            first = false;
            if (link.Missing is { } type)
            {
                ReportBlocks.Badge(builder, ReportBlocks.NotFound(type), "warning");
                continue;
            }

            if (link.Href is null)
            {
                builder.AddContent(1, link.Text);
                continue;
            }

            builder.OpenElement(2, "a");
            builder.AddAttribute(3, "href", link.Href);
            builder.AddAttribute(4, "class", "fx-link");
            builder.AddContent(5, link.Text);
            builder.CloseElement();
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^([A-Z][A-Za-z]+)/([A-Za-z0-9\-.]{1,64})$")]
    private static partial System.Text.RegularExpressions.Regex RelativeReference();

    private sealed record ReportLink(string Text, string? Href, string? Missing);
}

// The items of "each" as the rows of a table; its Columns are the cells, each with the item as %item (or the name of "as")
internal sealed class TableBlock : ReportBlock
{
    private IReadOnlyList<PocoNode> _items = [];
    private List<string> _titles = [];
    private string _name = ReportVariables.Item;

    protected override IEnumerable<ReportValue?> Reads => [Node.Attribute("each"), Node.Attribute("empty"), .. Node.Children.Select(column => column.Attribute("title"))];

    protected override void Prepare()
    {
        _items = Values.Nodes(Node.Attribute("each")!);
        _titles = [.. Node.Children.Select(column => Values.Text(column.Attribute("title")))];
        _name = Node.Attribute("as")?.Source is { Length: > 0 } name ? name : ReportVariables.Item;
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        if (_items.Count == 0)
        {
            if (Text("empty") is { Length: > 0 } empty)
            {
                builder.OpenElement(0, "p");
                builder.AddAttribute(1, "class", "fx-empty");
                builder.AddContent(2, empty);
                builder.CloseElement();
            }

            return;
        }

        builder.OpenElement(3, "div");
        builder.AddAttribute(4, "style", "overflow-x:auto");
        builder.OpenElement(5, "table");
        builder.AddAttribute(6, "class", "fx-table fx-table-sm");
        builder.OpenElement(7, "thead");
        builder.OpenElement(8, "tr");
        foreach (var title in _titles)
        {
            builder.OpenElement(9, "th");
            builder.AddAttribute(10, "scope", "col");
            builder.AddContent(11, title);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
        builder.OpenElement(12, "tbody");
        foreach (var item in _items)
        {
            builder.OpenElement(13, "tr");
            builder.AddContent(14, Scope.With(_name, [item]).Render(Node.Children));
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    }
}

// A cell of a table: "value" as text, then the inline content (its title is read by the table)
internal sealed class ColumnBlock : ReportBlock
{
    private string _value = string.Empty;

    protected override IEnumerable<ReportValue?> Reads => [Node.Attribute("value")];

    protected override void Prepare()
    {
        _value = Text("value");
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "td");
        builder.AddContent(1, _value);
        builder.AddContent(2, Children);
        builder.CloseElement();
    }
}

// Closed by default: what is there but not at the first look ("open" opens it)
internal sealed class DetailsBlock : ReportBlock
{
    private string _title = string.Empty;
    private bool _open;

    protected override void Prepare()
    {
        _title = Text("title");
        _open = Node.Attribute("open") is { } open && Values.IsTrue(open);
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "details");
        builder.AddAttribute(1, "class", "fx-other");
        builder.AddAttribute(2, "open", _open);
        builder.OpenElement(3, "summary");
        builder.AddContent(4, _title);
        builder.CloseElement();
        builder.AddContent(5, Children);
        builder.CloseElement();
    }
}

// What the reader must not miss: a warning, or a danger
internal sealed class NoticeBlock : ReportBlock
{
    private string _title = string.Empty;
    private string _tone = string.Empty;

    protected override void Prepare()
    {
        _title = Text("title");
        _tone = Text("tone") == "danger" ? "fx-notice-danger" : "fx-notice-warning";
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", $"fx-notice {_tone}");
        builder.AddAttribute(2, "role", "note");
        if (_title.Length > 0)
        {
            builder.OpenElement(3, "div");
            builder.AddAttribute(4, "class", "fx-notice-title");
            builder.AddContent(5, _title);
            builder.CloseElement();
        }

        builder.AddContent(6, Children);
        builder.CloseElement();
    }
}

// The records its references point to, read, as a variable of its content (the resolve of the templates); a reference that cannot be read is left
// out, and when none could, its part Otherwise is shown instead
internal sealed class ResolveBlock : ReportBlock
{
    private IReadOnlyList<PocoNode> _records = [];
    private string _name = ReportVariables.Record;

    protected override IEnumerable<ReportValue?> Reads => [Node.Attribute("value")];

    protected override async Task PrepareAsync()
    {
        _name = Node.Attribute("as")?.Source is { Length: > 0 } name ? name : ReportVariables.Record;
        var records = new List<PocoNode>();
        foreach (var node in Values.Nodes(Node.Attribute("value")!))
        {
            if (node.Poco is Resource)
            {
                records.Add(node);
            }
            else if (await Scope.Runtime.Data.ResolveAsync(node) is { } record)
            {
                records.Add(record);
            }
        }

        _records = records;
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        var otherwise = Node.Children.Where(child => child.Name == ReportSchema.Otherwise).ToList();
        builder.AddContent(0, _records.Count > 0
            ? Scope.With(_name, _records).Render(Node.Children.Where(child => child.Name != ReportSchema.Otherwise))
            : Scope.Render(otherwise.SelectMany(part => part.Children)));
    }
}

// Its parts that show something, joined by the separator (" · " by default), as the partial parts of the templates: an empty part drops its separator
internal sealed class PartsBlock : ReportBlock
{
    private List<ReportNode> _shown = [];
    private string _separator = " · ";

    protected override IEnumerable<ReportValue?> Reads => DeepReads;

    protected override void Prepare()
    {
        _separator = Node.Attribute("separator") is { } separator ? Values.Text(separator) : " · ";
        _shown = [.. Node.Children.Where(child => !IsEmpty(child))];
    }

    protected override void Build(RenderTreeBuilder builder)
    {
        for (var i = 0; i < _shown.Count; i++)
        {
            if (i > 0)
            {
                builder.AddContent(0, _separator);
            }

            builder.OpenElement(1, "bdi");
            builder.AddContent(2, Scope.Render([_shown[i]]));
            builder.CloseElement();
        }
    }
}
