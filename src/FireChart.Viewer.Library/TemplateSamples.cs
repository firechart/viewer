using Microsoft.Extensions.FileProviders;

namespace FireChart.Viewer.Library;

/// <summary>
/// The synthetic resources of the vendor library to preview and test the templates with: the static files <c>wwwroot/samples/{name}.json</c>. No real
/// patient data.
/// </summary>
/// <remarks>The administration of the templates reads them from the web root of its server (<see cref="Read"/>), a test from the folder.</remarks>
/// <example>
/// <code>
/// var samples = TemplateSamples.ReadFolder(@"public\src\FireChart.Viewer.Library\wwwroot");
/// var json = samples.Find("Patient-full");
/// </code>
/// </example>
public sealed class TemplateSamples
{
    /// <summary>
    /// The folder of the samples in the <c>wwwroot</c> of the library.
    /// </summary>
    public const string Folder = "samples";

    private const string Extension = ".json";

    private readonly Dictionary<string, string> _samples;

    private TemplateSamples(Dictionary<string, string> samples)
    {
        _samples = samples;
        Names = [.. samples.Keys.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The names of the samples (the file names without extension), in ordinal order.
    /// </summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// The JSON of a sample.
    /// </summary>
    /// <param name="name">The name (<c>Patient-full</c>).</param>
    /// <returns>The JSON, or <see langword="null"/> when there is no sample with the name.</returns>
    public string? Find(string name)
    {
        return _samples.GetValueOrDefault(name);
    }

    /// <summary>
    /// Reads the samples through a file provider (the web root of a server).
    /// </summary>
    /// <param name="files">The files of the host.</param>
    /// <param name="root">The folder of the library in it: <see cref="VendorLibrary.ContentRoot"/> in a web root.</param>
    /// <returns>The samples.</returns>
    /// <example>
    /// <code>
    /// var samples = TemplateSamples.Read(environment.WebRootFileProvider, VendorLibrary.ContentRoot);
    /// </code>
    /// </example>
    public static TemplateSamples Read(IFileProvider files, string root)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(root);
        var prefix = root.Trim('/');
        var samples = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in files.GetDirectoryContents(prefix.Length == 0 ? Folder : $"{prefix}/{Folder}").Where(entry => !entry.IsDirectory && entry.Name.EndsWith(Extension, StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(entry.CreateReadStream());
            samples[entry.Name[..^Extension.Length]] = reader.ReadToEnd();
        }

        return new TemplateSamples(samples);
    }

    /// <summary>
    /// Reads the samples from a folder of a disk.
    /// </summary>
    /// <param name="webRoot">The <c>wwwroot</c> of the project <c>FireChart.Viewer.Library</c>.</param>
    /// <returns>The samples.</returns>
    /// <example>
    /// <code>
    /// var samples = TemplateSamples.ReadFolder(@"public\src\FireChart.Viewer.Library\wwwroot");
    /// </code>
    /// </example>
    public static TemplateSamples ReadFolder(string webRoot)
    {
        var folder = Path.Combine(webRoot, Folder);
        return new TemplateSamples(Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*" + Extension).ToDictionary(file => Path.GetFileNameWithoutExtension(file), File.ReadAllText, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal));
    }
}
