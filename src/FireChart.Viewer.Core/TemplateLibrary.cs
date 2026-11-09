using Fluid;

namespace FireChart.Viewer.Core;

// Layers from the strongest to the weakest (client overrides first, the vendor library last).
// A template that does not parse is skipped, the next layer answers
internal sealed class TemplateLibrary(IReadOnlyList<ITemplateSource> layers, FluidParser parser, Action<string>? onProblem = null)
{
    private readonly Dictionary<string, ParsedTemplate> _parsed = [];

    public string? Find(string key)
    {
        return layers.Select(layer => layer.Find(key)).FirstOrDefault(text => text is not null);
    }

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
