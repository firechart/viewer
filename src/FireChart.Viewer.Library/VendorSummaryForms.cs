using System.Text.RegularExpressions;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;
using Microsoft.Extensions.FileProviders;

namespace FireChart.Viewer.Library;

/// <summary>
/// The clinical summary forms of the vendor library: the static files <c>wwwroot/summary-forms/{code}.json</c> (<see cref="VendorLibrary"/>);
/// <c>default</c> is the International Patient Summary and the form of a server that names none.
/// </summary>
/// <remarks>
/// A client reads the form of a code when it needs it (<see cref="FindAsync"/>, <see cref="VendorSummaryFormProvider"/>); a server or a test reads them
/// all (<see cref="Read(IFileProvider, string)"/>, <see cref="ReadFolder"/>). Every read gives a new instance, so a caller may change what it gets.
/// </remarks>
/// <example>
/// <code>
/// var form = await VendorSummaryForms.FindAsync(assets, ClinicalSummaryFormDefinition.DefaultCode) ?? throw new InvalidOperationException();
/// </code>
/// </example>
public static partial class VendorSummaryForms
{
    /// <summary>
    /// The folder of the forms in the <c>wwwroot</c> of the library.
    /// </summary>
    public const string Folder = "summary-forms";

    private const string Extension = ".json";

    /// <summary>
    /// The path of the file of a form in the library.
    /// </summary>
    /// <param name="code">The code of the form (<c>default</c>).</param>
    /// <returns>The path (<c>summary-forms/default.json</c>), or <see langword="null"/> for a code that cannot be a file name.</returns>
    /// <example>
    /// <code>
    /// var path = VendorSummaryForms.PathOf("default");
    /// </code>
    /// </example>
    public static string? PathOf(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return CodeRegex().IsMatch(code) ? $"{Folder}/{code}{Extension}" : null;
    }

    /// <summary>
    /// Reads the form of a code from the static files of the host.
    /// </summary>
    /// <param name="assets">Reads the static files of the host.</param>
    /// <param name="code">The code of the form.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text of the form, or <see langword="null"/> when the library has no form with the code.</returns>
    /// <exception cref="HttpRequestException">The file could not be read.</exception>
    /// <example>
    /// <code>
    /// var text = await VendorSummaryForms.TextAsync(assets, "default");
    /// </code>
    /// </example>
    public static async Task<string?> TextAsync(IViewerAssets assets, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);
        return PathOf(code) is { } path ? await assets.ReadTextAsync(VendorLibrary.UrlOf(path), cancellationToken) : null;
    }

    /// <summary>
    /// Reads and parses the form of a code from the static files of the host.
    /// </summary>
    /// <param name="assets">Reads the static files of the host.</param>
    /// <param name="code">The code of the form.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A new instance of the form, or <see langword="null"/> when the library has no form with the code.</returns>
    /// <exception cref="HttpRequestException">The file could not be read.</exception>
    /// <example>
    /// <code>
    /// var form = await VendorSummaryForms.FindAsync(assets, "default");
    /// </code>
    /// </example>
    public static async Task<ClinicalSummaryFormDefinition?> FindAsync(IViewerAssets assets, string code, CancellationToken cancellationToken = default)
    {
        return await TextAsync(assets, code, cancellationToken) is { } text ? ViewerJson.Read<ClinicalSummaryFormDefinition>(text) : null;
    }

    /// <summary>
    /// Reads all the forms through a file provider (the web root of a server).
    /// </summary>
    /// <param name="files">The files of the host.</param>
    /// <param name="root">The folder of the library in it: <see cref="VendorLibrary.ContentRoot"/> in a web root.</param>
    /// <returns>The texts of the forms by code.</returns>
    /// <example>
    /// <code>
    /// var texts = VendorSummaryForms.Read(environment.WebRootFileProvider, VendorLibrary.ContentRoot);
    /// </code>
    /// </example>
    public static IReadOnlyDictionary<string, string> Read(IFileProvider files, string root)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(root);
        var prefix = root.Trim('/');
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in files.GetDirectoryContents(prefix.Length == 0 ? Folder : $"{prefix}/{Folder}").Where(entry => !entry.IsDirectory && entry.Name.EndsWith(Extension, StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(entry.CreateReadStream());
            result[entry.Name[..^Extension.Length]] = reader.ReadToEnd();
        }

        return result;
    }

    /// <summary>
    /// Reads all the forms from a folder of a disk.
    /// </summary>
    /// <param name="webRoot">The <c>wwwroot</c> of the project <c>FireChart.Viewer.Library</c>.</param>
    /// <returns>The texts of the forms by code.</returns>
    /// <example>
    /// <code>
    /// var texts = VendorSummaryForms.ReadFolder(@"public\src\FireChart.Viewer.Library\wwwroot");
    /// </code>
    /// </example>
    public static IReadOnlyDictionary<string, string> ReadFolder(string webRoot)
    {
        var folder = Path.Combine(webRoot, Folder);
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*" + Extension).ToDictionary(file => Path.GetFileNameWithoutExtension(file), File.ReadAllText, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex CodeRegex();
}
