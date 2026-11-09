using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FireChart.Viewer.Appearance;

/// <summary>
/// The theme of the Viewer.
/// </summary>
public enum ViewerTheme
{
    /// <summary>The theme of the host document (<c>data-theme</c> on <c>&lt;html&gt;</c>); in a Flox application, the one of the application.</summary>
    Inherit,

    /// <summary>Light.</summary>
    Light,

    /// <summary>Dark.</summary>
    Dark,

    /// <summary>The one of the operating system (<c>prefers-color-scheme</c>).</summary>
    Auto
}

/// <summary>
/// A font the Viewer may use: an <c>@font-face</c>.
/// </summary>
/// <param name="Family">The name of the family (<c>Source Sans 3</c>).</param>
/// <param name="Src">The HTTPS URL of the font file (woff2).</param>
/// <param name="Weight">The weight or range (<c>400</c>, <c>100 900</c>).</param>
/// <param name="Style">The style (<c>normal</c>, <c>italic</c>).</param>
/// <example>
/// <code>
/// var font = new ViewerFont("Source Sans 3", "https://cdn.example.org/source-sans-3.woff2", "200 900");
/// </code>
/// </example>
public sealed record ViewerFont(string Family, string Src, string Weight = "400", string Style = "normal");

/// <summary>
/// How the Viewer looks: from the safest to the riskiest, the theme, the public variables, the rules of public selectors, the fonts and a free
/// stylesheet. Every part sets the tokens of the document the Viewer is drawn on (FloxWind, the Radzen variables, the <c>fx-*</c> kit); in a Flox
/// application the host usually sets none and the Viewer inherits its styles.
/// </summary>
/// <remarks>
/// The variables (<see cref="Variables"/>): <c>colorPrimary</c>, <c>colorBackground</c>, <c>colorSurface</c>, <c>colorText</c>,
/// <c>colorTextSecondary</c>, <c>colorBorder</c>, <c>colorDanger</c>, <c>colorWarning</c>, <c>colorSuccess</c>, <c>colorInfo</c>, <c>fontFamily</c>,
/// <c>borderRadius</c>. The selectors of the rules (<see cref="Rules"/>): <c>.Banner</c>, <c>.Tile</c>, <c>.Badge</c>, <c>.Card</c>, <c>.Table</c>.
/// A value that cannot be used is left out with a <see cref="ViewerConfigError"/> (<see cref="AppearanceCss.Compose"/>).
/// </remarks>
/// <example>
/// <code>
/// var appearance = new ViewerAppearance
/// {
///     Theme = ViewerTheme.Light,
///     Variables = { ["colorPrimary"] = "#0f766e", ["borderRadius"] = "4px" },
///     Rules = { [".Banner"] = new Dictionary&lt;string, string&gt; { ["backgroundColor"] = "#f0fdfa" } }
/// };
/// </code>
/// </example>
public sealed class ViewerAppearance
{
    /// <summary>The theme; <see cref="ViewerTheme.Inherit"/> by default.</summary>
    public ViewerTheme Theme { get; init; } = ViewerTheme.Inherit;

    /// <summary>The public variables by their camelCase names.</summary>
    public Dictionary<string, string> Variables { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The rules: a public selector (<c>.Tile</c>, <c>.Tile:hover</c>) and its properties by camelCase names.</summary>
    public Dictionary<string, Dictionary<string, string>> Rules { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The fonts to load.</summary>
    public List<ViewerFont> Fonts { get; init; } = [];

    /// <summary>Free CSS on the internal classes, the last layer: at the risk of the one who writes it (it may break with a new version).</summary>
    public string? AdvancedStylesheet { get; init; }
}

/// <summary>
/// An option of the configuration of the Viewer that cannot be used.
/// </summary>
/// <param name="Code">What is wrong: <c>unknown_option</c>, <c>invalid_value</c>.</param>
/// <param name="Param">The path of the option (<c>appearance.variables.colorPrimery</c>).</param>
/// <param name="Message">What is wrong, in English.</param>
public sealed record ViewerConfigError(string Code, string Param, string Message);

/// <summary>
/// The CSS of an appearance.
/// </summary>
/// <param name="Css">The stylesheet; empty when the appearance sets nothing.</param>
/// <param name="Errors">What was left out.</param>
public sealed record AppearanceStylesheet(string Css, IReadOnlyList<ViewerConfigError> Errors);

/// <summary>
/// Turns a <see cref="ViewerAppearance"/> into a stylesheet over the tokens of the document.
/// </summary>
/// <remarks>
/// The variables are written on <c>:root:root:root</c>, which outranks the palette of FloxWind (<c>html:root</c>, <c>html:root[data-theme]</c>) and
/// the stock Radzen theme whatever the order of the stylesheets. A colour gives its variants (light, dark, the soft tint) by <c>color-mix</c>. The
/// rules are in <c>@layer viewer-custom</c>; the free stylesheet comes last, outside the layers.
/// </remarks>
/// <example>
/// <code>
/// var sheet = AppearanceCss.Compose(appearance);
/// foreach (var error in sheet.Errors)
/// {
///     logger.LogWarning("{Param}: {Message}", error.Param, error.Message);
/// }
/// </code>
/// </example>
public static partial class AppearanceCss
{
    private const string Root = ":root:root:root";

    private static readonly Dictionary<string, string[]> s_status = new(StringComparer.Ordinal)
    {
        ["colorDanger"] = ["danger"],
        ["colorWarning"] = ["warning"],
        ["colorSuccess"] = ["success"],
        ["colorInfo"] = ["info"]
    };

    private static readonly Dictionary<string, string[]> s_plain = new(StringComparer.Ordinal)
    {
        ["colorBackground"] = ["--rz-body-background-color"],
        ["colorSurface"] = ["--rz-base-background-color"],
        ["colorText"] = ["--rz-text-color", "--rz-text-title-color"],
        ["colorTextSecondary"] = ["--rz-text-secondary-color"],
        ["colorBorder"] = ["--rz-border-color"]
    };

    // The public selectors and the classes they stand for
    private static readonly Dictionary<string, string> s_selectors = new(StringComparer.Ordinal)
    {
        [".Banner"] = ".fx-summary-patient",
        [".Tile"] = ".fx-tile",
        [".Badge"] = ".fx-badge",
        [".Card"] = ".fx-card",
        [".Table"] = ".fx-table"
    };

    private static readonly HashSet<string> s_states = new(StringComparer.Ordinal) { "", ":hover", ":focus", ":focus-visible" };

    // What a rule may set: colours, borders, radius, shadow, the font, spacing, the case and the spacing of letters
    private static readonly HashSet<string> s_properties = new(StringComparer.Ordinal)
    {
        "color", "backgroundColor", "borderColor", "borderWidth", "borderStyle", "borderRadius", "boxShadow", "fontFamily", "fontSize", "fontWeight",
        "fontStyle", "padding", "margin", "textTransform", "letterSpacing", "lineHeight", "outline", "outlineColor", "outlineOffset", "opacity"
    };

    [GeneratedRegex(@"^[#A-Za-z0-9(),.%\s/+\-]{1,120}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValuePattern();

    [GeneratedRegex(@"^[A-Za-z0-9 ,'""\-]{1,200}$", RegexOptions.CultureInvariant)]
    private static partial Regex FontPattern();

    [GeneratedRegex(@"^https://[^\s'""()<>\\]{1,500}$", RegexOptions.CultureInvariant)]
    private static partial Regex FontUrlPattern();

    /// <summary>
    /// Makes the stylesheet of an appearance.
    /// </summary>
    /// <param name="appearance">The appearance; <see langword="null"/> is none.</param>
    /// <returns>The CSS and what was left out.</returns>
    public static AppearanceStylesheet Compose(ViewerAppearance? appearance)
    {
        var errors = new List<ViewerConfigError>();
        if (appearance is null)
        {
            return new AppearanceStylesheet(string.Empty, errors);
        }

        var css = new StringBuilder();
        foreach (var font in appearance.Fonts)
        {
            AppendFont(css, font, errors);
        }

        var declarations = new StringBuilder();
        foreach (var (name, raw) in appearance.Variables)
        {
            AppendVariable(declarations, name, raw, errors);
        }

        if (declarations.Length > 0)
        {
            css.Append(Root).Append(" {\n").Append(declarations).Append("}\n");
        }

        var rules = new StringBuilder();
        foreach (var (selector, properties) in appearance.Rules)
        {
            AppendRule(rules, selector, properties, errors);
        }

        if (rules.Length > 0)
        {
            css.Append("@layer viewer-custom {\n").Append(rules).Append("}\n");
        }

        if (!string.IsNullOrWhiteSpace(appearance.AdvancedStylesheet))
        {
            // A style element ends at its closing tag: the text may not hold one
            if (appearance.AdvancedStylesheet.Contains("</", StringComparison.Ordinal))
            {
                errors.Add(new ViewerConfigError("invalid_value", "advanced.stylesheet", "The stylesheet may not contain '</'."));
            }
            else
            {
                css.Append(appearance.AdvancedStylesheet.Trim()).Append('\n');
            }
        }

        return new AppearanceStylesheet(css.ToString(), errors);
    }

    /// <summary>
    /// The value of the attribute <c>data-theme</c> of a theme.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="prefersDark">The operating system prefers dark (for <see cref="ViewerTheme.Auto"/>).</param>
    /// <returns><c>light</c> or <c>dark</c>; <see langword="null"/> for <see cref="ViewerTheme.Inherit"/> (the host decides).</returns>
    public static string? DataThemeOf(ViewerTheme theme, bool prefersDark)
    {
        return theme switch
        {
            ViewerTheme.Light => "light",
            ViewerTheme.Dark => "dark",
            ViewerTheme.Auto => prefersDark ? "dark" : "light",
            _ => null
        };
    }

    private static void AppendVariable(StringBuilder css, string name, string raw, List<ViewerConfigError> errors)
    {
        var param = $"appearance.variables.{name}";
        var value = raw?.Trim() ?? string.Empty;
        if (name == "fontFamily")
        {
            if (!FontPattern().IsMatch(value))
            {
                errors.Add(new ViewerConfigError("invalid_value", param, "A font family is names separated by commas."));
                return;
            }

            Declare(css, "--rz-text-font-family", value);
            Declare(css, "--font-sans", value);
            return;
        }

        if (!ValuePattern().IsMatch(value) || value.Contains("url", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ViewerConfigError("invalid_value", param, "The value is not a CSS colour or length."));
            return;
        }

        if (name == "borderRadius")
        {
            Declare(css, "--rz-border-radius", value);
            Declare(css, "--fx-radius", value);
            Declare(css, "--radius-field", value);
            Declare(css, "--radius-box", $"calc({value} * 2)");
            return;
        }

        if (name == "colorPrimary")
        {
            DeclareColor(css, "primary", value);
            Declare(css, "--rz-link-color", $"color-mix(in oklab, {value}, black 15%)");
            Declare(css, "--rz-link-hover-color", $"color-mix(in oklab, {value}, black 30%)");
            return;
        }

        if (s_status.TryGetValue(name, out var status))
        {
            DeclareColor(css, status[0], value);
            return;
        }

        if (s_plain.TryGetValue(name, out var targets))
        {
            foreach (var target in targets)
            {
                Declare(css, target, value);
            }

            return;
        }

        errors.Add(new ViewerConfigError("unknown_option", param, $"'{name}' is not a variable of the appearance."));
    }

    // A colour and the variants Radzen uses: light and dark by mixing, the soft tint by transparency
    private static void DeclareColor(StringBuilder css, string role, string value)
    {
        Declare(css, $"--rz-{role}", value);
        Declare(css, $"--rz-{role}-light", $"color-mix(in oklab, {value}, white 15%)");
        Declare(css, $"--rz-{role}-dark", $"color-mix(in oklab, {value}, black 15%)");
        Declare(css, $"--rz-{role}-darker", $"color-mix(in oklab, {value}, black 30%)");
        Declare(css, $"--rz-{role}-lighter", $"color-mix(in oklab, {value} 12%, transparent)");
        Declare(css, $"--rz-on-{role}-lighter", $"color-mix(in oklab, {value}, black 30%)");
    }

    private static void AppendRule(StringBuilder css, string selector, Dictionary<string, string> properties, List<ViewerConfigError> errors)
    {
        var colon = selector.IndexOf(':', StringComparison.Ordinal);
        var name = colon < 0 ? selector : selector[..colon];
        var state = colon < 0 ? string.Empty : selector[colon..];
        if (!s_selectors.TryGetValue(name, out var internalName) || !s_states.Contains(state))
        {
            errors.Add(new ViewerConfigError("unknown_option", $"appearance.rules.{selector}", $"'{selector}' is not a selector of the appearance."));
            return;
        }

        var body = new StringBuilder();
        foreach (var (property, raw) in properties)
        {
            var param = $"appearance.rules.{selector}.{property}";
            var value = raw?.Trim() ?? string.Empty;
            if (!s_properties.Contains(property))
            {
                errors.Add(new ViewerConfigError("unknown_option", param, $"'{property}' may not be set by a rule."));
            }
            else if (property == "fontFamily" ? !FontPattern().IsMatch(value) : !ValuePattern().IsMatch(value) || value.Contains("url", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ViewerConfigError("invalid_value", param, "The value cannot be used."));
            }
            else
            {
                Declare(body, Kebab(property), value);
            }
        }

        if (body.Length > 0)
        {
            css.Append("  ").Append(internalName).Append(state).Append(" {\n").Append(body).Append("  }\n");
        }
    }

    private static void AppendFont(StringBuilder css, ViewerFont font, List<ViewerConfigError> errors)
    {
        if (!FontPattern().IsMatch(font.Family) || font.Family.Contains(',', StringComparison.Ordinal) || !FontUrlPattern().IsMatch(font.Src)
            || !ValuePattern().IsMatch(font.Weight) || font.Style is not ("normal" or "italic"))
        {
            errors.Add(new ViewerConfigError("invalid_value", $"appearance.fonts.{font.Family}", "A font needs a family name, an https URL, a weight and a style."));
            return;
        }

        css.Append(CultureInfo.InvariantCulture, $"@font-face {{ font-family: \"{font.Family.Trim('"', '\'')}\"; src: url(\"{font.Src}\"); font-weight: {font.Weight}; font-style: {font.Style}; font-display: swap; }}\n");
    }

    private static void Declare(StringBuilder css, string property, string value)
    {
        css.Append("  ").Append(property).Append(": ").Append(value).Append(";\n");
    }

    private static string Kebab(string camel)
    {
        var result = new StringBuilder(camel.Length + 4);
        foreach (var c in camel)
        {
            if (char.IsUpper(c))
            {
                result.Append('-').Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
