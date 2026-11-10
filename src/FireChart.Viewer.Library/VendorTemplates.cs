using System.Text.Json;
using FireChart.Viewer.Core;
using Microsoft.Extensions.FileProviders;

namespace FireChart.Viewer.Library;

/// <summary>
/// The vendor library of templates and the translations of its texts, read whole from the files of the library: on a server (its web root) or in a test
/// (the folder of the project). A client loads them one by one instead (<see cref="WebVendorTemplates"/>).
/// </summary>
/// <remarks>
/// The templates are the <c>.liquid</c> files of <c>wwwroot/templates</c>; the key is the file name without extension (<c>Patient@tile</c>). The translations
/// are <c>wwwroot/locales/{language}/fhir-templates.json</c> in the format of i18next (the English text and its translation). Put it last among the layers
/// of a <see cref="FhirRenderer"/>: the layers before it override its templates and texts key by key. The files are read once, when it is created.
/// </remarks>
/// <example>
/// <code>
/// var vendor = VendorTemplates.FromFolder(Path.Combine(projectFolder, "wwwroot"));
/// var renderer = new FhirRenderer([customerOverrides, vendor]);
/// </code>
/// </example>
public sealed class VendorTemplates : ITemplateSource, ITemplateTexts
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _texts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the library through a file provider.
    /// </summary>
    /// <param name="files">The files of the host (the web root of a server: <c>IWebHostEnvironment.WebRootFileProvider</c>).</param>
    /// <param name="root">The folder of the library in it: <see cref="VendorLibrary.ContentRoot"/> in a web root, empty for the <c>wwwroot</c> of the project.</param>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> or <paramref name="root"/> is <see langword="null"/>.</exception>
    public VendorTemplates(IFileProvider files, string root)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(root);
        var prefix = root.Trim('/');
        prefix = prefix.Length == 0 ? string.Empty : prefix + "/";
        ReadTemplates(files, prefix + VendorLibrary.TemplatesFolder);
        foreach (var language in files.GetDirectoryContents(prefix + VendorLibrary.LocalesFolder).Where(entry => entry.IsDirectory))
        {
            var file = files.GetFileInfo($"{prefix}{VendorLibrary.LocalePathOf(language.Name)}");
            if (file.Exists)
            {
                using var reader = new StreamReader(file.CreateReadStream());
                _texts[language.Name] = Parse(reader.ReadToEnd());
            }
        }
    }

    /// <summary>
    /// Reads the library from a folder of a disk.
    /// </summary>
    /// <param name="webRoot">The <c>wwwroot</c> of the project <c>FireChart.Viewer.Library</c>.</param>
    /// <returns>The library.</returns>
    /// <exception cref="DirectoryNotFoundException">The folder has no templates.</exception>
    /// <example>
    /// <code>
    /// var vendor = VendorTemplates.FromFolder(@"public\src\FireChart.Viewer.Library\wwwroot");
    /// </code>
    /// </example>
    public static VendorTemplates FromFolder(string webRoot)
    {
        var templates = Path.Combine(webRoot, VendorLibrary.TemplatesFolder);
        if (!Directory.Exists(templates))
        {
            throw new DirectoryNotFoundException($"No templates in '{webRoot}'.");
        }

        var library = new VendorTemplates();
        foreach (var file in Directory.EnumerateFiles(templates, "*" + VendorLibrary.TemplateExtension, SearchOption.AllDirectories))
        {
            library._templates[Path.GetFileNameWithoutExtension(file)] = File.ReadAllText(file);
        }

        var locales = Path.Combine(webRoot, VendorLibrary.LocalesFolder);
        foreach (var folder in Directory.Exists(locales) ? Directory.EnumerateDirectories(locales) : [])
        {
            var file = Path.Combine(folder, VendorLibrary.LocaleFile);
            if (File.Exists(file))
            {
                library._texts[Path.GetFileName(folder)] = Parse(File.ReadAllText(file));
            }
        }

        return library;
    }

    private VendorTemplates()
    {
    }

    private static Dictionary<string, string> Parse(string json)
    {
        return new Dictionary<string, string>(JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [], StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public IEnumerable<string> Keys => _templates.Keys;

    /// <summary>
    /// The languages that have translations (<c>de</c>, <c>ru</c>...).
    /// </summary>
    public IEnumerable<string> Languages => _texts.Keys;

    /// <inheritdoc/>
    public string? Find(string key)
    {
        return _templates.GetValueOrDefault(key);
    }

    /// <inheritdoc/>
    public string? Translate(string culture, string key)
    {
        return _texts.TryGetValue(culture, out var texts) && texts.TryGetValue(key, out var text) && text.Length > 0 ? text : null;
    }

    private void ReadTemplates(IFileProvider files, string folder)
    {
        foreach (var entry in files.GetDirectoryContents(folder))
        {
            if (entry.IsDirectory)
            {
                ReadTemplates(files, $"{folder}/{entry.Name}");
            }
            else if (entry.Name.EndsWith(VendorLibrary.TemplateExtension, StringComparison.Ordinal))
            {
                using var reader = new StreamReader(entry.CreateReadStream());
                _templates[entry.Name[..^VendorLibrary.TemplateExtension.Length]] = reader.ReadToEnd();
            }
        }
    }
}
