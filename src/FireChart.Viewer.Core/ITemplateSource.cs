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
/// var renderer = new FhirRenderer([overrides, new VendorTemplates()]);
/// </code>
/// </example>
public interface ITemplateSource
{
    /// <summary>
    /// The keys of all the templates of the layer.
    /// </summary>
    IEnumerable<string> Keys { get; }

    /// <summary>
    /// Finds the Liquid text of a template.
    /// </summary>
    /// <param name="key">The key of the template, the file name without extension (<c>Patient@tile</c>).</param>
    /// <returns>The text of the template, or <see langword="null"/> when the layer has no template with this key.</returns>
    string? Find(string key);
}
