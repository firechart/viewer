namespace FireChart.Viewer.Core;

/// <summary>
/// A layer of templates held in memory: the overrides that came from a server, a file of a customer, or a test.
/// </summary>
/// <param name="templates">The texts of the templates by key (file name without extension, <c>Patient@tile</c>).</param>
/// <example>
/// <code>
/// var overrides = new MemoryTemplateSource(new Dictionary&lt;string, string&gt; { ["Coding@text"] = "{{ self.code }}" });
/// var renderer = new FhirRenderer([overrides, new VendorTemplates()]);
/// </code>
/// </example>
public sealed class MemoryTemplateSource(IReadOnlyDictionary<string, string> templates) : ITemplateSource
{
    /// <summary>
    /// Creates an empty layer.
    /// </summary>
    public MemoryTemplateSource() : this(new Dictionary<string, string>())
    {
    }

    /// <inheritdoc/>
    public IEnumerable<string> Keys => templates.Keys;

    /// <inheritdoc/>
    public string? Find(string key)
    {
        return templates.GetValueOrDefault(key);
    }
}
