using System.Text.RegularExpressions;
using FireChart.Viewer.Core;
using Microsoft.Extensions.FileProviders;

namespace FireChart.Viewer.Library;

/// <summary>
/// A design token of the kit: a custom property <c>--fx-*</c> and its vendor value.
/// </summary>
/// <param name="Name">The name (<c>--fx-color-accent</c>).</param>
/// <param name="Value">The value in the vendor style.</param>
/// <example>
/// <code>
/// foreach (var token in sheets.Tokens())
/// {
///     Console.WriteLine($"{token.Name}: {token.Value}");
/// }
/// </code>
/// </example>
public sealed record FxToken(string Name, string Value);

/// <summary>
/// The vendor style of the kit fx-*: <c>wwwroot/styles/fx.css</c> (the classes and the tokens of the kit) and <c>wwwroot/styles/fx.daisy.css</c> (the subset of
/// daisyUI 5 with the prefix fx-, built by <c>styles/daisy/build.cmd</c>).
/// </summary>
/// <param name="Vendor">The text of <c>fx.css</c>.</param>
/// <param name="Daisy">The text of <c>fx.daisy.css</c>.</param>
/// <remarks>Read by a client from the static files of its host (<see cref="FxStyle.LoadAsync"/>), by a server from its web root, by a test from the folder.</remarks>
/// <example>
/// <code>
/// var sheets = await FxStyle.LoadAsync(assets);
/// var css = sheets.Compose(customerCss);
/// </code>
/// </example>
public sealed partial record FxStyleSheets(string Vendor, string Daisy)
{
    /// <summary>
    /// The style of the visualization: the vendor style in the layer <see cref="FxStyle.VendorLayer"/>, the custom one in <see cref="FxStyle.CustomLayer"/>,
    /// so a custom rule wins over a vendor one whatever their specificity.
    /// </summary>
    /// <param name="custom">A style over the vendor one (the layer <see cref="FxStyle.CustomLayer"/>); none: the vendor style only. The theme of the
    /// Viewer is not passed here: it is a style of the whole document (<c>AppearanceCss</c> of the Viewer).</param>
    /// <returns>The CSS text.</returns>
    /// <example>
    /// <code>
    /// var css = sheets.Compose(".fx-root { --fx-gap: 1rem; }");
    /// </code>
    /// </example>
    public string Compose(string? custom)
    {
        var text = $"@layer {FxStyle.VendorLayer}, {FxStyle.CustomLayer};\n@layer {FxStyle.VendorLayer} {{\n{Daisy}\n{Vendor}\n}}\n";
        return string.IsNullOrWhiteSpace(custom) ? text : $"{text}@layer {FxStyle.CustomLayer} {{\n{custom}\n}}\n";
    }

    /// <summary>
    /// The design tokens of the vendor style, in the order of their first declaration.
    /// </summary>
    /// <returns>The tokens, each once.</returns>
    /// <example>
    /// <code>
    /// var accent = sheets.Tokens().First(token => token.Name == "--fx-color-accent").Value;
    /// </code>
    /// </example>
    public IReadOnlyList<FxToken> Tokens()
    {
        return [.. TokenRegex().Matches(Vendor).Select(match => new FxToken(match.Groups[1].Value, match.Groups[2].Value.Trim())).DistinctBy(token => token.Name)];
    }

    [GeneratedRegex(@"(--fx-[a-z0-9-]+)\s*:\s*([^;]+);")]
    private static partial Regex TokenRegex();
}

/// <summary>
/// What a theme of the Viewer is made of: the base (<c>styles/fx.themed.css</c>: the vendor palette as values and the bridge from the tokens to the
/// Radzen variables) and the file of the theme (<c>styles/themes/{name}.css</c>: the tokens <c>--fx-*</c>, light and dark).
/// </summary>
/// <param name="Base">The text of <c>fx.themed.css</c>.</param>
/// <param name="Theme">The text of the theme; <see langword="null"/> when none was asked for or the library has no such file.</param>
/// <example>
/// <code>
/// var theme = await FxStyle.LoadThemeAsync(assets, "fluent");
/// if (theme.Theme is null)
/// {
///     Console.WriteLine("No such theme");
/// }
/// </code>
/// </example>
public sealed record FxThemeSheets(string Base, string? Theme);

/// <summary>
/// Where the vendor style is and how it is read (<see cref="FxStyleSheets"/>, <see cref="FxThemeSheets"/>).
/// </summary>
/// <example>
/// <code>
/// var css = (await FxStyle.LoadAsync(assets)).Compose(null);
/// </code>
/// </example>
public static partial class FxStyle
{
    /// <summary>
    /// The cascade layer of the vendor style.
    /// </summary>
    public const string VendorLayer = "fx";

    /// <summary>
    /// The cascade layer of the style of the customer.
    /// </summary>
    public const string CustomLayer = "fx-custom";

    /// <summary>
    /// The folder of the style in the <c>wwwroot</c> of the library.
    /// </summary>
    public const string Folder = "styles";

    /// <summary>
    /// The folder of the themes in the folder of the style: <c>styles/themes/{name}.css</c>.
    /// </summary>
    public const string ThemesFolder = "themes";

    private const string VendorFile = "fx.css";
    private const string DaisyFile = "fx.daisy.css";
    private const string ThemedFile = "fx.themed.css";

    /// <summary>
    /// Whether a name may name a theme: lowercase letters, digits and hyphens, up to 40 (a part of a path, never a path).
    /// </summary>
    /// <param name="name">The name (<c>fluent</c>).</param>
    /// <returns><see langword="true"/> for a name a theme may have.</returns>
    /// <example>
    /// <code>
    /// var ok = FxStyle.IsThemeName("fluent"); // true; "../x" and "Fluent" are not
    /// </code>
    /// </example>
    public static bool IsThemeName(string? name)
    {
        return name is not null && ThemeNamePattern().IsMatch(name);
    }

    /// <summary>
    /// The path of a theme in the library.
    /// </summary>
    /// <param name="name">The name of the theme (<see cref="IsThemeName"/>).</param>
    /// <returns><c>styles/themes/{name}.css</c>.</returns>
    /// <exception cref="ArgumentException">The name cannot name a theme.</exception>
    /// <example>
    /// <code>
    /// var path = FxStyle.ThemePathOf("fluent"); // styles/themes/fluent.css
    /// </code>
    /// </example>
    public static string ThemePathOf(string name)
    {
        return IsThemeName(name) ? $"{Folder}/{ThemesFolder}/{name}.css" : throw new ArgumentException($"'{name}' cannot name a theme.", nameof(name));
    }

    /// <summary>
    /// Reads the base of a theme and the file of a theme from the static files of the host.
    /// </summary>
    /// <param name="assets">Reads the static files of the host.</param>
    /// <param name="name">The name of the theme; none, or a name that cannot name one: the base only.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The base and the theme (<see langword="null"/> when the library has no such file).</returns>
    /// <exception cref="InvalidOperationException">The host does not serve the base of the themes.</exception>
    /// <exception cref="HttpRequestException">A file could not be read.</exception>
    /// <example>
    /// <code>
    /// var theme = await FxStyle.LoadThemeAsync(assets, "fluent");
    /// </code>
    /// </example>
    public static async Task<FxThemeSheets> LoadThemeAsync(IViewerAssets assets, string? name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var themed = assets.ReadTextAsync(VendorLibrary.UrlOf($"{Folder}/{ThemedFile}"), cancellationToken);
        var theme = IsThemeName(name) ? assets.ReadTextAsync(VendorLibrary.UrlOf(ThemePathOf(name!)), cancellationToken) : Task.FromResult<string?>(null);
        return new FxThemeSheets(await themed ?? throw Missing(ThemedFile), await theme);
    }

    /// <summary>
    /// Reads the base of a theme and the file of a theme from a folder of a disk.
    /// </summary>
    /// <param name="webRoot">The folder of the library (<c>public/library</c>).</param>
    /// <param name="name">The name of the theme; none: the base only.</param>
    /// <returns>The base and the theme (<see langword="null"/> when there is no such file).</returns>
    /// <example>
    /// <code>
    /// var theme = FxStyle.ReadThemeFolder(@"public\library", "compact");
    /// </code>
    /// </example>
    public static FxThemeSheets ReadThemeFolder(string webRoot, string? name)
    {
        var path = IsThemeName(name) ? Path.Combine(webRoot, Folder, ThemesFolder, $"{name}.css") : null;
        return new FxThemeSheets(File.ReadAllText(Path.Combine(webRoot, Folder, ThemedFile)), path is not null && File.Exists(path) ? File.ReadAllText(path) : null);
    }

    /// <summary>
    /// Reads the vendor style from the static files of the host.
    /// </summary>
    /// <param name="assets">Reads the static files of the host.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The style.</returns>
    /// <exception cref="InvalidOperationException">The host does not serve the files of the style.</exception>
    /// <exception cref="HttpRequestException">A file could not be read.</exception>
    /// <example>
    /// <code>
    /// var sheets = await FxStyle.LoadAsync(assets);
    /// </code>
    /// </example>
    public static async Task<FxStyleSheets> LoadAsync(IViewerAssets assets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var vendor = assets.ReadTextAsync(VendorLibrary.UrlOf($"{Folder}/{VendorFile}"), cancellationToken);
        var daisy = assets.ReadTextAsync(VendorLibrary.UrlOf($"{Folder}/{DaisyFile}"), cancellationToken);
        return new FxStyleSheets(await vendor ?? throw Missing(VendorFile), await daisy ?? throw Missing(DaisyFile));
    }

    /// <summary>
    /// Reads the vendor style through a file provider (the web root of a server).
    /// </summary>
    /// <param name="files">The files of the host.</param>
    /// <param name="root">The folder of the library in it: <see cref="VendorLibrary.ContentRoot"/> in a web root.</param>
    /// <returns>The style.</returns>
    /// <exception cref="InvalidOperationException">The files of the style are not there.</exception>
    /// <example>
    /// <code>
    /// var sheets = FxStyle.Read(environment.WebRootFileProvider, VendorLibrary.ContentRoot);
    /// </code>
    /// </example>
    public static FxStyleSheets Read(IFileProvider files, string root)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(root);
        var prefix = root.Trim('/');
        prefix = prefix.Length == 0 ? Folder : $"{prefix}/{Folder}";

        string Text(string name)
        {
            var file = files.GetFileInfo($"{prefix}/{name}");
            if (!file.Exists)
            {
                throw Missing(name);
            }

            using var reader = new StreamReader(file.CreateReadStream());
            return reader.ReadToEnd();
        }

        return new FxStyleSheets(Text(VendorFile), Text(DaisyFile));
    }

    /// <summary>
    /// Reads the vendor style from a folder of a disk.
    /// </summary>
    /// <param name="webRoot">The <c>wwwroot</c> of the project <c>FireChart.Viewer.Library</c>.</param>
    /// <returns>The style.</returns>
    /// <example>
    /// <code>
    /// var sheets = FxStyle.ReadFolder(@"public\library");
    /// </code>
    /// </example>
    public static FxStyleSheets ReadFolder(string webRoot)
    {
        return new FxStyleSheets(File.ReadAllText(Path.Combine(webRoot, Folder, VendorFile)), File.ReadAllText(Path.Combine(webRoot, Folder, DaisyFile)));
    }

    private static InvalidOperationException Missing(string name)
    {
        return new InvalidOperationException($"The vendor style {name} is not served by the host.");
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeNamePattern();
}
