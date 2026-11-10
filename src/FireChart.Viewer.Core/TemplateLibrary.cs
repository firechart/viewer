using System.Text.RegularExpressions;
using Fluid;

namespace FireChart.Viewer.Core;

// Layers from the strongest to the weakest (client overrides first, the vendor library last).
// A template that does not parse is skipped, the next layer answers
internal sealed partial class TemplateLibrary(IReadOnlyList<ITemplateSource> layers, FluidParser parser, Action<string>? onProblem = null)
{
    private readonly Dictionary<string, ParsedTemplate> _parsed = [];

    public string? Find(string key)
    {
        return layers.Select(layer => layer.Find(key)).FirstOrDefault(text => text is not null);
    }

    // The first layer that has the key, loading it on demand
    public async ValueTask<string?> FindAsync(string key, CancellationToken cancellationToken = default)
    {
        foreach (var layer in layers)
        {
            if (await layer.FindAsync(key, cancellationToken) is { } text)
            {
                return text;
            }
        }

        return null;
    }

    // ResolveKeyed with the templates loaded on demand: the keys in order until one has a template that parses (the weaker ones are not loaded),
    // with the partials it renders, so the render finds them all in memory
    public async ValueTask<ResolvedTemplate?> ResolveKeyedAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var chain = keys.ToList();
        foreach (var key in chain)
        {
            if (await FindAsync(key, cancellationToken) is not { } text)
            {
                continue;
            }

            await LoadPartialsAsync(text, [], cancellationToken);
            if (parser.TryParse(text, out _, out _))
            {
                break;
            }
        }

        return ResolveKeyed(chain);
    }

    // {% render "name" %}: Fluid reads a partial through the file provider, synchronously, so it is loaded with the template that renders it
    private async ValueTask LoadPartialsAsync(string text, HashSet<string> seen, CancellationToken cancellationToken)
    {
        // {% slot_data %} renders the partials of the context line of a tile itself
        var names = RenderRegex().Matches(text).Select(match => match.Groups["name"].Value);
        if (SlotDataRegex().IsMatch(text))
        {
            names = names.Concat(SlotDataPartials);
        }

        foreach (var file in names)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (seen.Add(name) && await FindAsync(name, cancellationToken) is { } partial)
            {
                await LoadPartialsAsync(partial, seen, cancellationToken);
            }
        }
    }

    [GeneratedRegex("""\{%-?\s*(?:render|include)\s+["'](?<name>[^"']+)["']""")]
    private static partial Regex RenderRegex();

    private static readonly string[] SlotDataPartials = ["tile-patient", "tile-encounter", "age-at"];

    [GeneratedRegex("""\{%-?\s*slot_data""")]
    private static partial Regex SlotDataRegex();

    public IFluidTemplate? Resolve(IEnumerable<string> keys)
    {
        return ResolveKeyed(keys)?.Template;
    }

    // The template and the key it was found by (the engine needs to know when the generic template of a structure answered)
    public ResolvedTemplate? ResolveKeyed(IEnumerable<string> keys)
    {
        List<string>? skipped = null;
        foreach (var key in keys)
        {
            foreach (var layer in layers)
            {
                var text = layer.Find(key);
                if (text is null)
                {
                    continue;
                }

                var template = Parse(layer, key, text, out var error);
                if (template is not null)
                {
                    return new ResolvedTemplate(key, template, skipped ?? []);
                }

                (skipped ??= []).Add($"Template '{key}' is skipped: {error}");
            }
        }

        return null;
    }

    private IFluidTemplate? Parse(ITemplateSource layer, string key, string text, out string? error)
    {
        var id = $"{layers.ToList().IndexOf(layer)}:{key}";
        lock (_parsed)
        {
            if (_parsed.TryGetValue(id, out var cached) && cached.Text == text)
            {
                error = cached.Error;
                return cached.Template;
            }

            if (!parser.TryParse(text, out var template, out error))
            {
                onProblem?.Invoke($"Template '{key}' is skipped: {error}");
                template = null;
            }

            _parsed[id] = new ParsedTemplate(text, template, error);
            return template;
        }
    }

    private sealed record ParsedTemplate(string Text, IFluidTemplate? Template, string? Error);

    // Skipped: why the stronger templates of the keys were not used (a syntax error), for the page to show
    public sealed record ResolvedTemplate(string Key, IFluidTemplate Template, IReadOnlyList<string> Skipped);
}
