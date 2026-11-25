using System.Collections.Immutable;
using System.Globalization;
using FireChart.Viewer.Core.Reports;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Components;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Reports;

// What one report shares: its components (those of the modes join as they are needed), its data, its culture; and the bound of the depth
internal sealed class ReportRuntime(IReadOnlyDictionary<string, ComponentDefinition> components, ReportData data, ReportFormat format,
                                    Func<string, Task<ReportBundle>>? load = null)
{
    public const int MaxDepth = 32;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ComponentDefinition> _components = new(components, StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Task<ComponentDefinition?>>> _modes = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ComponentDefinition> Components => _components;

    public ReportData Data => data;

    // Grows when what the blocks read besides their data has changed (words of codes have come): a block drawn again with the same node and scope
    // reads its values again only then
    public int Generation => _generation;

    private int _generation;

    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
    }

    public CultureInfo Culture => format.Culture;

    // The culture, the zone of the display and the words of the codes of the texts
    public ReportFormat Format => format;

    // The component of a type in a mode ({Type}.{mode}: Patient.banner), read with the components it calls; null when the library has none (the
    // built-in view then). One with problems throws them, so the block shows them
    public Task<ComponentDefinition?> ModeAsync(string type, string mode)
    {
        var name = $"{type}.{mode}";
        if (_components.TryGetValue(name, out var known))
        {
            return Task.FromResult<ComponentDefinition?>(known);
        }

        return load is null ? Task.FromResult<ComponentDefinition?>(null) : _modes.GetOrAdd(name, key => new Lazy<Task<ComponentDefinition?>>(() => LoadModeAsync(key))).Value;
    }

    private async Task<ComponentDefinition?> LoadModeAsync(string name)
    {
        var bundle = await load!(name);
        if (bundle.Missing)
        {
            return null;
        }

        if (bundle.Problems.Length > 0)
        {
            throw new InvalidDataException(string.Join("; ", bundle.Problems));
        }

        foreach (var (key, component) in bundle.Components)
        {
            _components.TryAdd(key, component);
        }

        return bundle.Root;
    }
}

// What a node sees: the variables (%context, the parameters, the item of a Repeat, the results of the searches; each a task until it answers) and,
// in a component of the project, the slots its caller filled. A component gets a new scope with its parameters only; the content of a slot is
// rendered in the scope of the caller, where it was written
internal sealed class ReportScope
{
    private readonly ImmutableDictionary<string, Task<IReadOnlyList<PocoNode>>> _variables;

    private ReportScope(ReportRuntime runtime, ImmutableDictionary<string, Task<IReadOnlyList<PocoNode>>> variables,
                        ImmutableDictionary<string, ImmutableArray<ReportNode>> slots, ReportScope? slotOwner, int depth, TileOptions? options = null)
    {
        Runtime = runtime;
        Options = options ?? TileOptions.Default;
        _variables = variables;
        Slots = slots;
        SlotOwner = slotOwner;
        Depth = depth;
    }

    public ReportRuntime Runtime { get; }

    public ImmutableDictionary<string, ImmutableArray<ReportNode>> Slots { get; }

    public ReportScope? SlotOwner { get; }

    public int Depth { get; }

    // What the tiles of the records of a section show besides their components (Feed: age, status, codes)
    public TileOptions Options { get; }

    public ReportScope WithOptions(TileOptions options)
    {
        return new ReportScope(Runtime, _variables, Slots, SlotOwner, Depth, options);
    }

    // The scope of a page; a page without a record of its own (a list) has no %context
    public static ReportScope Root(ReportRuntime runtime, PocoNode? context)
    {
        var variables = ImmutableDictionary<string, Task<IReadOnlyList<PocoNode>>>.Empty;
        return new ReportScope(runtime, context is null ? variables : variables.Add(ReportVariables.Context, Ready([context])),
                               ImmutableDictionary<string, ImmutableArray<ReportNode>>.Empty, null, 0);
    }

    public ReportScope With(string name, IReadOnlyList<PocoNode> value)
    {
        return new ReportScope(Runtime, _variables.SetItem(name, Ready(value)), Slots, SlotOwner, Depth, Options);
    }

    // The scope of the body of a component: the context of the report, its parameters, the slots filled by the caller (this scope)
    public ReportScope ForComponent(IEnumerable<KeyValuePair<string, Task<IReadOnlyList<PocoNode>>>> parameters, ImmutableDictionary<string, ImmutableArray<ReportNode>> slots)
    {
        var variables = (_variables.TryGetValue(ReportVariables.Context, out var context)
                             ? ImmutableDictionary<string, Task<IReadOnlyList<PocoNode>>>.Empty.Add(ReportVariables.Context, context)
                             : ImmutableDictionary<string, Task<IReadOnlyList<PocoNode>>>.Empty).SetItems(parameters);
        return new ReportScope(Runtime, variables, slots, this, Depth + 1, Options);
    }

    // The searches of a component start as soon as their query can be written (its variables have answered); each is a variable named by its id
    public ReportScope WithSearches(IEnumerable<SearchDefinition> searches)
    {
        var variables = _variables;
        foreach (var search in searches)
        {
            variables = variables.SetItem(search.Id, SearchAsync(search));
        }

        return new ReportScope(Runtime, variables, Slots, SlotOwner, Depth, Options);
    }

    private async Task<IReadOnlyList<PocoNode>> SearchAsync(SearchDefinition search)
    {
        var values = await ResolveAsync([search.Query]);
        return await Runtime.Data.SearchAsync(values.Text(search.Query), search.Max);
    }

    // The value of a parameter: one expression alone gives its elements (when they come), anything else is a string
    public async Task<IReadOnlyList<PocoNode>> ArgumentAsync(ReportValue value)
    {
        return (await ResolveAsync([value])).Nodes(value);
    }

    // Whether the variables these values name have all answered (a block shows its content at once instead of a skeleton)
    public bool IsReady(IEnumerable<ReportValue?> values)
    {
        return Names(values).All(name => !_variables.TryGetValue(name, out var task) || task.IsCompleted);
    }

    // Waits for the variables the values name, and only those: a block is not held up by a search it does not read. A failed search throws
    public async Task<ReportValues> ResolveAsync(IEnumerable<ReportValue?> values)
    {
        var resolved = new Dictionary<string, IReadOnlyList<PocoNode>>(StringComparer.Ordinal);
        foreach (var name in Names(values).Append(ReportVariables.Context).Distinct())
        {
            if (_variables.TryGetValue(name, out var task))
            {
                resolved[name] = await task;
            }
        }

        // The rows of a section with decimals write their numbers with at most that many; the others by the FHIR settings of the Viewer
        return new ReportValues(resolved, Options.Decimals is { } places ? Runtime.Format.WithDecimals(places) : Runtime.Format);
    }

    public RenderFragment Render(IEnumerable<ReportNode> nodes)
    {
        return ReportRenderer.Render(nodes, this);
    }

    private static IEnumerable<string> Names(IEnumerable<ReportValue?> values)
    {
        return values.OfType<ReportValue>().SelectMany(value => value.Expressions).SelectMany(expression => expression.Variables);
    }

    private static Task<IReadOnlyList<PocoNode>> Ready(IReadOnlyList<PocoNode> value)
    {
        return Task.FromResult(value);
    }
}

// The variables a block reads, answered: what its values evaluate to
internal sealed class ReportValues(IReadOnlyDictionary<string, IReadOnlyList<PocoNode>> variables, ReportFormat culture)
{
    public IReadOnlyList<PocoNode> Evaluate(ReportExpression expression, PocoNode? focus = null)
    {
        return ReportExpressions.Evaluate(expression, focus, variables);
    }

    // The record of the report (%context)
    public PocoNode? Context => variables.TryGetValue(ReportVariables.Context, out var context) && context.Count > 0 ? context[0] : null;

    // Whether a reference points to the record of the report (%context): a tile leaves it out of its second line
    public bool IsContext(PocoNode reference)
    {
        if (!variables.TryGetValue(ReportVariables.Context, out var context) || context is not [{ Poco: Resource { Id: { } id } resource }])
        {
            return false;
        }

        var target = ReportText.Children(reference, "reference").FirstOrDefault()?.Poco is PrimitiveType value ? value.JsonValue?.ToString() : null;
        return target is not null && (target == $"{resource.TypeName}/{id}" || target.EndsWith($"/{resource.TypeName}/{id}", StringComparison.Ordinal));
    }

    // An element as text, in the culture of the report
    public string TextOf(PocoNode node)
    {
        return ReportText.Of(node, culture);
    }

    // The elements of a value as texts joined by a separator, the empty ones left out
    public string Join(ReportValue value, string separator)
    {
        return string.Join(separator, Nodes(value).Select(TextOf).Where(text => text.Length > 0));
    }

    public IReadOnlyList<PocoNode> Nodes(ReportValue value)
    {
        return value.Single is { } expression ? Evaluate(expression) : [ReportExpressions.String(Text(value))];
    }

    public string Text(ReportValue? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return string.Concat(value.Parts.Select(part => part switch
        {
            ReportLiteral literal => literal.Text,
            ReportString text => culture.Strings.Format(text.Key, [.. text.Arguments.Select(argument => ReportText.Of(Evaluate(argument), culture))], culture.Culture),
            ReportExpression expression => ReportText.Of(Evaluate(expression), culture),
            _ => string.Empty
        }));
    }

    // FHIRPath's own truth: true when the result is a single true, or anything that is not a boolean
    public bool IsTrue(ReportValue value)
    {
        return ReportExpressions.IsTrue(Nodes(value));
    }
}
