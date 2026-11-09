namespace FireChart.Viewer.Library;

/// <summary>
/// Synthetic resources of the library: what the preview of a template renders and what the tests of the templates use.
/// </summary>
/// <remarks>
/// The embedded <c>src/FireChart.Viewer.Library/samples/*.json</c>; the name is the file name without extension. The data are synthetic: no real patient.
/// </remarks>
/// <example>
/// <code>
/// foreach (var name in TemplateSamples.Names)
/// {
///     var row = await renderer.RenderTileAsync(TemplateSamples.Find(name)!);
/// }
/// </code>
/// </example>
public static class TemplateSamples
{
    private const string Extension = ".json";

    private static readonly Dictionary<string, string> s_samples = Load();

    /// <summary>
    /// The names of the samples, in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = [.. s_samples.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// Finds a sample.
    /// </summary>
    /// <param name="name">The name of the sample, the file name without extension.</param>
    /// <returns>The resource as JSON text, or <see langword="null"/> when there is no such sample.</returns>
    public static string? Find(string name)
    {
        return s_samples.GetValueOrDefault(name);
    }

    private static Dictionary<string, string> Load()
    {
        var assembly = typeof(TemplateSamples).Assembly;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(Extension, StringComparison.Ordinal) && !n.StartsWith("locales.", StringComparison.Ordinal) && !n.StartsWith("summary-forms.", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            result[name[..^Extension.Length]] = reader.ReadToEnd();
        }

        return result;
    }
}
