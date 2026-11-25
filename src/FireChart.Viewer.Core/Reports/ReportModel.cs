using System.Collections.Immutable;

namespace FireChart.Viewer.Core.Reports;

// A node of a component as parsed: an element with its attributes and children, or a text (Name is ReportNode.TextName)
internal sealed record ReportNode(string Name, ImmutableDictionary<string, ReportValue> Attributes, ImmutableArray<ReportNode> Children, int Line)
{
    public const string TextName = "#text";

    public ReportValue? Attribute(string name)
    {
        return Attributes.GetValueOrDefault(name);
    }

    public ReportValue? Text => Name == TextName ? Attributes.GetValueOrDefault(TextName) : null;
}

// The value of an attribute or a text: literal parts and FHIRPath expressions between {{ }}. A value that is one expression alone gives its
// elements (a resource, a list); any other is a string
internal sealed record ReportValue(string Source, ImmutableArray<ReportPart> Parts)
{
    public ReportExpression? Single => Parts is [ReportExpression expression] ? expression : null;

    // The expressions of the value, the arguments of its texts too
    public IEnumerable<ReportExpression> Expressions => Parts.SelectMany(part => part switch
    {
        ReportExpression expression => [expression],
        ReportString text => text.Arguments,
        _ => Enumerable.Empty<ReportExpression>()
    });

    public IEnumerable<ReportString> Strings => Parts.OfType<ReportString>();
}

internal abstract record ReportPart;

internal sealed record ReportLiteral(string Text) : ReportPart;

// The variables an expression names (%context, %labs) are known after parsing: a block waits for the data sources among them
internal sealed record ReportExpression(string Source, ImmutableArray<string> Variables) : ReportPart;

// A text of the resources of the library ({{%strings.Key}}, {{%strings.Key(%a, %b)}}): its key, and the expressions that fill {0}, {1}
internal sealed record ReportString(string Key, ImmutableArray<ReportExpression> Arguments) : ReportPart;

// A component of a designer project (components/{Name}.xml): its parameters, slots, searches and body
internal sealed record ComponentDefinition(
    string Name,
    ImmutableArray<ParamDefinition> Params,
    ImmutableArray<string> Slots,
    ImmutableArray<SearchDefinition> Data,
    ImmutableArray<ReportNode> Body,
    int Line);

internal sealed record ParamDefinition(string Name, bool Required, int Line);

// A search of a component: its id is the variable (%labs) of its results; the query is the part of a FHIR search after the base, with expressions
internal sealed record SearchDefinition(string Id, ReportValue Query, int Max, int Line);

// What is wrong with a definition: where (the component, the line) and what
internal sealed record ReportProblem(string Component, int Line, string Message)
{
    public override string ToString()
    {
        return $"{Component}:{Line}: {Message}";
    }
}
