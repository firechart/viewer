namespace FireChart.Viewer.Core;

/// <summary>
/// A layer of the template library: finds the Liquid text of a template by its key.
/// </summary>
/// <remarks>
/// The key is the file name of the template without its extension: <c>Patient</c>, <c>Patient@tile</c>, <c>Coding@text</c>, <c>badge</c>.
/// A <see cref="FhirRenderer"/> takes several layers and looks a key up from the first layer to the last, so a layer placed before the vendor library
/// overrides its templates one by one. A layer may also add keys the vendor library does not have (a template of a profile of its own).
/// </remarks>
/// <example>
/// <code>
/// ITemplateSource overrides = new MemoryTemplateSource(new Dictionary&lt;string, string&gt;
/// {
///     ["Patient@tile"] = "&lt;li class=\"fx-list-row\"&gt;{{ self.name | first | apply: \"text\" }}&lt;/li&gt;"
/// });
/// var renderer = new FhirRenderer([overrides, new WebVendorTemplates(assets)]);
/// </code>
/// </example>
public interface ITemplateSource
{
    /// <summary>
    /// The keys of all the templates of the layer; a layer that loads its templates on demand knows only the ones it has loaded.
    /// </summary>
    IEnumerable<string> Keys { get; }

    /// <summary>
    /// Finds the Liquid text of a template among the ones the layer has at hand (in memory, on a disk), without waiting for anything.
    /// </summary>
    /// <param name="key">The key of the template, the file name without extension (<c>Patient@tile</c>).</param>
    /// <returns>The text of the template, or <see langword="null"/> when the layer has no template with this key at hand.</returns>
    string? Find(string key);

    /// <summary>
    /// Finds the Liquid text of a template, loading it when the layer loads its templates on demand (over HTTP from the static files of the host).
    /// </summary>
    /// <param name="key">The key of the template, the file name without extension (<c>Patient@tile</c>).</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The text of the template, or <see langword="null"/> when the layer has no template with this key.</returns>
    /// <remarks>The engine asks for a template when it needs it; once found, <see cref="Find"/> has it too. By default the same as <see cref="Find"/>.</remarks>
    /// <example>
    /// <code>
    /// var text = await layer.FindAsync("Patient@tile");
    /// </code>
    /// </example>
    ValueTask<string?> FindAsync(string key, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Find(key));
    }
}
