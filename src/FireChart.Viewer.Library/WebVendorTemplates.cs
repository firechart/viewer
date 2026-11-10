using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using FireChart.Viewer.Core;

namespace FireChart.Viewer.Library;

/// <summary>
/// The vendor library of templates for a client: every template and the translations of a language are read from the static files of the host when the
/// engine first needs them (<see cref="VendorLibrary"/>), and kept.
/// </summary>
/// <remarks>
/// <see cref="FindAsync"/> loads a template (a missing one is remembered too, so it is not asked for again); <see cref="Find"/> and <see cref="Keys"/>
/// know the loaded ones only. The engine loads a template with the partials it renders before it renders it. The addresses carry the version of the
/// library, so the browser may cache them.
/// </remarks>
/// <param name="assets">Reads the static files of the host.</param>
/// <example>
/// <code>
/// var renderer = new FhirRenderer([customerOverrides, new WebVendorTemplates(assets)]);
/// var html = await renderer.RenderTileAsync(json);   // loads StructureDefinition@tile, parts, badge... once
/// </code>
/// </example>
public sealed class WebVendorTemplates(IViewerAssets assets) : ITemplateSource, ITemplateTexts
{
    private const string English = "en";

    // What is loaded (null: the library has no such file), and what is being loaded: one request per file whoever asks
    private readonly ConcurrentDictionary<string, string?> _templates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<string?>> _loadingTemplates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Dictionary<string, string>?> _texts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<Dictionary<string, string>?>> _loadingTexts = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public IEnumerable<string> Keys => _templates.Where(pair => pair.Value is not null).Select(pair => pair.Key);

    /// <inheritdoc/>
    public string? Find(string key)
    {
        return _templates.GetValueOrDefault(key);
    }

    /// <inheritdoc/>
    public async ValueTask<string?> FindAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_templates.TryGetValue(key, out var known))
        {
            return known;
        }

        var loading = _loadingTemplates.GetOrAdd(key, LoadTemplateAsync);
        try
        {
            var text = await loading.WaitAsync(cancellationToken);
            _templates[key] = text;
            return text;
        }
        finally
        {
            // a load that failed (the network) is tried again next time
            if (loading.IsCompleted)
            {
                _loadingTemplates.TryRemove(new KeyValuePair<string, Task<string?>>(key, loading));
            }
        }
    }

    /// <inheritdoc/>
    public string? Translate(string culture, string key)
    {
        return _texts.TryGetValue(culture, out var texts) && texts is not null && texts.TryGetValue(key, out var text) && text.Length > 0 ? text : null;
    }

    /// <inheritdoc/>
    public async ValueTask PrepareAsync(CultureInfo culture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(culture);
        // The folders of the library are languages (ru, zh), not cultures: the language of the culture, then English
        foreach (var language in new[] { culture.TwoLetterISOLanguageName, English }.Where(name => name.Length > 0 && name != "iv").Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_texts.ContainsKey(language))
            {
                continue;
            }

            var loading = _loadingTexts.GetOrAdd(language, LoadTextsAsync);
            try
            {
                _texts[language] = await loading.WaitAsync(cancellationToken);
            }
            catch (HttpRequestException)
            {
                // the texts of the language are not shown this time, the next render asks again
            }
            finally
            {
                if (loading.IsCompleted)
                {
                    _loadingTexts.TryRemove(new KeyValuePair<string, Task<Dictionary<string, string>?>>(language, loading));
                }
            }
        }
    }

    private async Task<string?> LoadTemplateAsync(string key)
    {
        return VendorLibrary.TemplatePathOf(key) is { } path ? await assets.ReadTextAsync(VendorLibrary.UrlOf(path), CancellationToken.None) : null;
    }

    private async Task<Dictionary<string, string>?> LoadTextsAsync(string language)
    {
        if (VendorLibrary.LocalePathOf(language) is not { } path || await assets.ReadTextAsync(VendorLibrary.UrlOf(path), CancellationToken.None) is not { } json)
        {
            return null;
        }

        return new Dictionary<string, string>(JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [], StringComparer.Ordinal);
    }
}
