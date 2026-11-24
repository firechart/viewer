using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace FireChart.Viewer.Core.Reports;

// The components of the library: components/{Name}.xml, read through the assets of the Viewer (the vendor's, a customer's over them file by file);
// locate turns a path of the library into the address the assets read (the vendor library: library/{path}?v=…). A component is read with every
// component it calls and checked as a whole; a file is read and parsed once for every library over the same assets (the pages, the lists, the
// search forms each make one), a file the library does not have too, so it is asked for once
internal sealed partial class ReportLibrary(IViewerAssets assets, Func<string, string> locate)
{
    public const string Folder = "components";

    // What the libraries over the same assets have read, by the address of the file
    private static readonly ConditionalWeakTable<IViewerAssets, Read> s_read = new();

    private readonly ConcurrentDictionary<string, Lazy<Task<Loaded>>> _files = s_read.GetValue(assets, _ => new Read()).Files;
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyDictionary<string, string>?>>> _resources = s_read.GetValue(assets, _ => new Read()).Resources;

    private sealed class Read
    {
        public ConcurrentDictionary<string, Lazy<Task<Loaded>>> Files { get; } = new(StringComparer.Ordinal);

        public ConcurrentDictionary<string, Lazy<Task<IReadOnlyDictionary<string, string>?>>> Resources { get; } = new(StringComparer.Ordinal);
    }

    // The texts of the components in a language: locales/strings.resx, then the files of the culture and its parents over it (he over English,
    // he-IL over he); a file the library does not have is skipped
    public async Task<ReportStrings> StringsAsync(System.Globalization.CultureInfo culture, CancellationToken cancellationToken = default)
    {
        var files = new List<IReadOnlyDictionary<string, string>>();
        foreach (var name in ReportStrings.ChainOf(culture))
        {
            if (await ResourcesAsync(name, cancellationToken) is { } file)
            {
                files.Add(file);
            }
        }

        return ReportStrings.Of(files);
    }

    // The keys of locales/strings.resx, which the components may name; null when the library has no texts (they are not checked then)
    public async Task<IReadOnlySet<string>?> KeysAsync(CancellationToken cancellationToken = default)
    {
        return await ResourcesAsync(string.Empty, cancellationToken) is { } texts ? texts.Keys.ToHashSet(StringComparer.Ordinal) : null;
    }

    private Task<IReadOnlyDictionary<string, string>?> ResourcesAsync(string culture, CancellationToken cancellationToken)
    {
        // Read to the end whoever asked first: what is kept serves every later caller, a cancelled read would fail them all
        return _resources.GetOrAdd(locate(ReportStrings.PathOf(culture)), address => new Lazy<Task<IReadOnlyDictionary<string, string>?>>(async () =>
        {
            try
            {
                return await assets.ReadTextAsync(address, CancellationToken.None) is { } text ? ReportStrings.ReadResx(text) : null;
            }
            catch (Exception ex) when (ex is InvalidDataException or HttpRequestException)
            {
                return null;
            }
        })).Value.WaitAsync(cancellationToken);
    }

    public static string PathOf(string name)
    {
        return NameRegex().IsMatch(name) ? $"{Folder}/{name}.xml" : throw new ArgumentException($"\"{name}\" is not a name of a component", nameof(name));
    }

    public async Task<ReportBundle> LoadAsync(string name, CancellationToken cancellationToken = default)
    {
        var components = new Dictionary<string, ComponentDefinition>(StringComparer.Ordinal);
        var problems = new List<ReportProblem>();
        var pending = new Queue<string>([name]);
        while (pending.TryDequeue(out var next))
        {
            if (components.ContainsKey(next) || problems.Any(problem => problem.Component == next))
            {
                continue;
            }

            var loaded = await FileAsync(next, cancellationToken);
            if (loaded.Component is null && next == name && loaded.Missing)
            {
                return new ReportBundle(null, ImmutableDictionary<string, ComponentDefinition>.Empty, [new ReportProblem(name, 0, loaded.Error!)]) { Missing = true };
            }

            if (loaded.Component is null)
            {
                problems.Add(new ReportProblem(next, loaded.Line, loaded.Error!));
                continue;
            }

            components[next] = loaded.Component;
            if (Forms.SearchFormComponents.IsSearchForm(next))
            {
                continue;
            }

            foreach (var called in Called(loaded.Component.Body))
            {
                pending.Enqueue(called);
            }
        }

        if (!components.TryGetValue(name, out var root))
        {
            return new ReportBundle(null, components.ToImmutableDictionary(), [.. problems]);
        }

        var keys = await KeysAsync(cancellationToken);
        foreach (var component in components.Values)
        {
            problems.AddRange(ReportCheck.Check(component, components.GetValueOrDefault, keys));
        }

        return new ReportBundle(root, components.ToImmutableDictionary(), [.. problems.Distinct()]);
    }

    // A component given as it is (not a file) with the components it calls, checked as LoadAsync checks
    public static ReportBundle Check(ComponentDefinition root, IReadOnlyDictionary<string, ComponentDefinition> components)
    {
        var all = new Dictionary<string, ComponentDefinition>(components, StringComparer.Ordinal) { [root.Name] = root };
        var problems = all.Values.SelectMany(component => ReportCheck.Check(component, all.GetValueOrDefault)).Distinct();
        return new ReportBundle(root, all.ToImmutableDictionary(), [.. problems]);
    }

    // By the address of its file; read to the end whoever asked first (what is kept serves every later caller)
    private Task<Loaded> FileAsync(string name, CancellationToken cancellationToken)
    {
        var address = NameRegex().IsMatch(name) ? locate(PathOf(name)) : name;
        return _files.GetOrAdd(address, _ => new Lazy<Task<Loaded>>(() => ReadAsync(name, CancellationToken.None))).Value.WaitAsync(cancellationToken);
    }
    private async Task<Loaded> ReadAsync(string name, CancellationToken cancellationToken)
    {
        if (!NameRegex().IsMatch(name))
        {
            return new Loaded(null, 0, $"\"{name}\" is not a name of a component");
        }

        var xml = await assets.ReadTextAsync(locate(PathOf(name)), cancellationToken);
        if (xml is null)
        {
            return new Loaded(null, 0, $"no component {name} ({PathOf(name)})") { Missing = true };
        }

        try
        {
            var component = ReportXml.Parse(xml);
            return component.Name == name
                ? new Loaded(component, component.Line, null)
                : new Loaded(null, component.Line, $"the file {PathOf(name)} names its component \"{component.Name}\"");
        }
        catch (InvalidDataException ex)
        {
            return new Loaded(null, 0, ex.Message);
        }
    }

    private static IEnumerable<string> Called(IEnumerable<ReportNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Name != ReportNode.TextName && node.Name != ReportSchema.Fill && !ReportSchema.BuiltIns.ContainsKey(node.Name))
            {
                yield return node.Name;
            }

            foreach (var called in Called(node.Children))
            {
                yield return called;
            }
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9.-]*$")]
    private static partial Regex NameRegex();

    private sealed record Loaded(ComponentDefinition? Component, int Line, string? Error)
    {
        public bool Missing { get; init; }
    }
}

// A component with every component it calls, checked; Root is null when the component itself could not be read
internal sealed record ReportBundle(ComponentDefinition? Root, ImmutableDictionary<string, ComponentDefinition> Components, ImmutableArray<ReportProblem> Problems)
{
    // The library has no file of the component itself
    public bool Missing { get; init; }
}
