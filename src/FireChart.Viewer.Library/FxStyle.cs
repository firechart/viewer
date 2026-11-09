using System.Text.RegularExpressions;

namespace FireChart.Viewer.Library;

/// <summary>
/// A token of the vendor style: a custom property <c>--fx-*</c> and its value.
/// </summary>
/// <param name="Name">The name of the custom property (<c>--fx-color-primary</c>).</param>
/// <param name="Value">Its value in the vendor style.</param>
/// <example>
/// <code>
/// var primary = FxStyle.Tokens().First(token =&gt; token.Name == "--fx-color-primary").Value;
/// </code>
/// </example>
public sealed record FxToken(string Name, string Value);

/// <summary>
/// The style of the visualization: the vendor style with the style of a customer over it.
/// </summary>
/// <remarks>
/// The vendor style (the daisyUI components of the kit, then <c>fx.css</c>) is in the cascade layer <see cref="VendorLayer"/>, the customer's in
/// <see cref="CustomLayer"/>, a later layer, so the customer's wins whatever the specificity of the selectors. Check the customer's style with
/// <see cref="StyleChecks.Check"/> before it is composed.
/// </remarks>
/// <example>
/// <code>
/// var css = FxStyle.Compose(".fx-banner { border-radius: 0 }");
/// // &lt;style&gt; with css in the page
/// </code>
/// </example>
public static partial class FxStyle
{
    /// <summary>
    /// The cascade layer of the vendor style: <c>fx</c>.
    /// </summary>
    public const string VendorLayer = "fx";

    /// <summary>
    /// The cascade layer of the style of a customer: <c>fx-custom</c>.
    /// </summary>
    public const string CustomLayer = "fx-custom";

    private const string VendorResource = "fx.css";
    private const string DaisyResource = "fx.daisy.css";

    private static readonly Lazy<string> s_vendor = new(() => Read(VendorResource));
    private static readonly Lazy<string> s_daisy = new(() => Read(DaisyResource));

    /// <summary>
    /// <c>fx.css</c>: the tokens and the classes of the kit.
    /// </summary>
    public static string Vendor => s_vendor.Value;

    /// <summary>
    /// The daisyUI components of the kit (the classes <c>fx-badge</c>, <c>fx-card</c>, <c>fx-list</c>...), built from <c>src/FireChart.Viewer.Library/styles/daisy</c>;
    /// <see cref="Vendor"/> comes after it and wins.
    /// </summary>
    public static string Daisy => s_daisy.Value;

    /// <summary>
    /// Composes the style of the page: the order of the layers, the vendor style in its layer, the customer's style in the later layer.
    /// </summary>
    /// <param name="custom">The style of the customer, already checked by <see cref="StyleChecks.Check"/>; <see langword="null"/> or white space for none.</param>
    /// <returns>The CSS text.</returns>
    public static string Compose(string? custom)
    {
        var text = $"@layer {VendorLayer}, {CustomLayer};\n@layer {VendorLayer} {{\n{Daisy}\n{Vendor}\n}}\n";
        return string.IsNullOrWhiteSpace(custom) ? text : $"{text}@layer {CustomLayer} {{\n{custom}\n}}\n";
    }

    /// <summary>
    /// The tokens (<c>--fx-*</c>) the vendor style defines, with their values: what a customer changes first.
    /// </summary>
    /// <returns>The tokens in the order of the vendor style, each name once.</returns>
    public static IReadOnlyList<FxToken> Tokens()
    {
        return [.. Token().Matches(Vendor).Select(match => new FxToken(match.Groups[1].Value, match.Groups[2].Value.Trim())).DistinctBy(token => token.Name)];
    }

    [GeneratedRegex(@"(--fx-[a-z0-9-]+)\s*:\s*([^;]+);")]
    private static partial Regex Token();

    private static string Read(string resource)
    {
        using var stream = typeof(FxStyle).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
