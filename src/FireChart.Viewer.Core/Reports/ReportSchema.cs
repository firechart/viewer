using System.Collections.Frozen;

namespace FireChart.Viewer.Core.Reports;

// What a built-in component takes: its attributes, which children, and the only parent it may have. The Blazor block of each is in FireChart.Viewer
// (ReportBlocks); the schema is here so a definition can be checked without a browser (the tests, later the designer and the upload of a package)
internal sealed record BuiltInSchema(string Name, FrozenDictionary<string, bool> Attributes, ReportChildren Children)
{
    // The children allowed when Children is Only
    public FrozenSet<string> Only { get; init; } = FrozenSet<string>.Empty;

    // The block may stand only in this one (a Column in a Table)
    public string? Parent { get; init; }

    public bool Allows(string attribute)
    {
        return Attributes.ContainsKey(attribute);
    }

    public IEnumerable<string> RequiredAttributes => Attributes.Where(attribute => attribute.Value).Select(attribute => attribute.Key);
}

internal enum ReportChildren
{
    // No children at all
    None,
    // Text and inline blocks (Value, Badge)
    Inline,
    // Anything
    Any,
    // The blocks of BuiltInSchema.Only
    Only
}

internal static class ReportSchema
{
    public const string Section = "Section";
    public const string Card = "Card";
    public const string Columns = "Columns";
    public const string Field = "Field";
    public const string Value = "Value";
    public const string Age = "Age";
    public const string Badge = "Badge";
    public const string Text = "Text";
    public const string Repeat = "Repeat";
    public const string If = "If";
    public const string SlotOutlet = "SlotOutlet";
    public const string Row = "Row";
    public const string Status = "Status";
    public const string Interpretation = "Interpretation";
    public const string Link = "Link";
    public const string Table = "Table";
    public const string Column = "Column";
    public const string Details = "Details";
    public const string Notice = "Notice";
    public const string Show = "Show";
    public const string Resolve = "Resolve";
    public const string Parts = "Parts";
    public const string Subtitle = "Subtitle";
    public const string Main = "Main";
    public const string Otherwise = "Otherwise";
    public const string Feed = "Feed";
    public const string Query = "Query";
    public const string Tile = "Tile";
    public const string Banner = "Banner";
    public const string Facts = "Facts";
    public const string Flags = "Flags";
    public const string Leading = "Leading";
    public const string Trailing = "Trailing";

    // A call of a component of the project gives the content of a named slot inside <Fill slot="…">; anything else is the default slot
    public const string Fill = "Fill";

    public static readonly FrozenDictionary<string, BuiltInSchema> BuiltIns = new BuiltInSchema[]
    {
        Of(Section, ReportChildren.Any, ("title", false), ("count", false)),
        Of(Card, ReportChildren.Any, ("title", false)),
        Of(Columns, ReportChildren.Any),
        Of(Field, ReportChildren.Inline, ("label", true), ("value", false)),
        Of(Value, ReportChildren.None, ("value", true), ("empty", false), ("separator", false)),
        Of(Age, ReportChildren.None, ("birth", true), ("at", false)),
        Of(Badge, ReportChildren.Inline, ("text", false), ("tone", false)),
        Of(Text, ReportChildren.Inline, ("tone", false)),
        Of(Repeat, ReportChildren.Any, ("each", true), ("as", false), ("empty", false)),
        Of(If, ReportChildren.Any, ("test", true)),
        Of(SlotOutlet, ReportChildren.None, ("name", false)),
        Of(Row, ReportChildren.Inline, ("main", false), ("meta", false)),
        Of(Status, ReportChildren.None, ("value", true), ("label", false)),
        Of(Interpretation, ReportChildren.None, ("value", true)),
        Of(Link, ReportChildren.None, ("value", true), ("text", false)),
        Of(Table, ReportChildren.Only, ("each", true), ("as", false), ("empty", false)) with { Only = new[] { Column }.ToFrozenSet() },
        Of(Column, ReportChildren.Inline, ("title", true), ("value", false)) with { Parent = Table },
        Of(Details, ReportChildren.Any, ("title", true), ("open", false)),
        Of(Notice, ReportChildren.Any, ("title", false), ("tone", false)),
        Of(Show, ReportChildren.None, ("value", true), ("mode", true)),
        Of(Resolve, ReportChildren.Any, ("value", true), ("as", false)),
        Of(Parts, ReportChildren.Inline, ("separator", false)),
        Of(Subtitle, ReportChildren.Inline) with { Parent = Tile },
        Of(Main, ReportChildren.Inline) with { Parent = Row },
        Of(Otherwise, ReportChildren.Any) with { Parent = Resolve },
        Of(Feed, ReportChildren.Only, ("title", true), ("code", false), ("collapsed", false), ("hideIfEmpty", false), ("hideIfUnsupported", false),
           ("open", false), ("display", false), ("decimals", false), ("age", false), ("status", false), ("codes", false)) with { Only = new[] { Query }.ToFrozenSet() },
        Of(Query, ReportChildren.None, ("type", true), ("params", false), ("param", false), ("sort", false), ("pageSize", false)) with { Parent = Feed },
        Of(Tile, ReportChildren.Only, ("title", true), ("subtitle", false), ("patient", false), ("encounter", false)) with { Only = new[] { Leading, Subtitle, Trailing }.ToFrozenSet() },
        Of(Leading, ReportChildren.Inline, ("icon", false)) with { Parent = Tile },
        Of(Trailing, ReportChildren.Inline) with { Parent = Tile },
        Of(Banner, ReportChildren.Only, ("title", true)) with { Only = new[] { Facts, Flags }.ToFrozenSet() },
        Of(Facts, ReportChildren.Inline) with { Parent = Banner },
        Of(Flags, ReportChildren.Inline) with { Parent = Banner },
    }.ToFrozenDictionary(schema => schema.Name);

    // The inline blocks: what a Field, a Badge, a Row or a Column may hold besides text
    public static readonly FrozenSet<string> Inline = new[] { Value, Age, Badge, Text, If, Status, Interpretation, Link, Show, Parts, Main }.ToFrozenSet();

    // How Show draws an element: the component {Type}.{mode} of the library, else the built-in view of the mode
    public static readonly FrozenSet<string> Modes = new[] { "text", "link", "tile", "banner", "view" }.ToFrozenSet();

    // The tones of a badge, a text or a notice: the colours of the kit
    public static readonly FrozenSet<string> Tones = new[] { "neutral", "info", "success", "warning", "danger" }.ToFrozenSet();

    private static BuiltInSchema Of(string name, ReportChildren children, params (string Name, bool Required)[] attributes)
    {
        return new BuiltInSchema(name, attributes.ToFrozenDictionary(attribute => attribute.Name, attribute => attribute.Required), children);
    }
}
