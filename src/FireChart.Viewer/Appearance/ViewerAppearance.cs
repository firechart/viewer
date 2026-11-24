using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FireChart.Viewer.Library;

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
/// How the Viewer looks: from the safest to the riskiest, the light or dark theme, a style (a theme file of the library: the tokens <c>--fx-*</c>), the
/// public variables, the rules of public selectors, the fonts and a free stylesheet. A style or a variable makes the Viewer its own: the tokens drive the
/// content and, through <c>styles/fx.themed.css</c>, the components of Blazor Blueprint and the utilities around it, on the whole document. In a Flox application the
/// host sets none and the Viewer inherits its palette.
/// </summary>
/// <remarks>
/// The style (<see cref="Style"/>) is the name of a file <c>styles/themes/{name}.css</c> of the library: the vendor ships <c>fluent</c> and
/// <c>compact</c>, an installation adds its own in its appearance folder. The variables (<see cref="Variables"/>) set single tokens over it:
/// <c>colorPrimary</c>, <c>colorBackground</c>, <c>colorSurface</c>, <c>colorText</c>, <c>colorTextSecondary</c>, <c>colorBorder</c>,
/// <c>colorDanger</c>, <c>colorWarning</c>, <c>colorSuccess</c>, <c>colorInfo</c>, <c>fontFamily</c>, <c>borderRadius</c>. The selectors of the rules
/// (<see cref="Rules"/>): <c>.Banner</c>, <c>.Tile</c>, <c>.Badge</c>, <c>.Card</c>, <c>.Table</c>. A value that cannot be used is left out with a
/// <see cref="ViewerConfigError"/> (<see cref="AppearanceCss.Compose"/>).
/// </remarks>
/// <example>
/// <code>
/// var appearance = new ViewerAppearance
/// {
///     Theme = ViewerTheme.Light,
///     Style = "fluent",
///     Variables = { ["colorPrimary"] = "#0f766e", ["borderRadius"] = "4px" },
///     Rules = { [".Banner"] = new Dictionary&lt;string, string&gt; { ["backgroundColor"] = "#f0fdfa" } }
/// };
/// </code>
/// </example>
public sealed class ViewerAppearance
{
    /// <summary>The theme; <see cref="ViewerTheme.Inherit"/> by default.</summary>
    public ViewerTheme Theme { get; init; } = ViewerTheme.Inherit;

    /// <summary>The name of a theme file of the library, <c>styles/themes/{name}.css</c> (<c>fluent</c>, <c>compact</c>); none: the vendor look.</summary>
    public string? Style { get; init; }

    /// <summary>The public variables by their camelCase names: single tokens over the style.</summary>
    public Dictionary<string, string> Variables { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The rules: a public selector (<c>.Tile</c>, <c>.Tile:hover</c>) and its properties by camelCase names.</summary>
    public Dictionary<string, Dictionary<string, string>> Rules { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The fonts to load; a style or <c>fontFamily</c> names them.</summary>
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
/// Turns a <see cref="ViewerAppearance"/> into a stylesheet over the tokens of the kit (<c>--fx-*</c>).
/// </summary>
/// <remarks>
/// The order: the fonts; the base of a theme (<see cref="FxThemeSheets.Base"/>: the vendor palette as values and the bridge from the tokens to the
/// tokens of Blazor Blueprint) when the appearance has a style or variables; the style in the layer <c>fx-custom</c>; the variables as tokens on
/// <c>:root:root:root</c>, outside the layers, so they win over the style; the rules in <c>fx-custom</c>; the free stylesheet last, outside the layers.
/// The order of the layers <c>fx, fx-custom</c> is declared first, so the vendor style of a page (<see cref="FxStyleSheets.Compose"/>) keeps it.
/// </remarks>
/// <example>
/// <code>
/// var theme = AppearanceCss.IsThemed(appearance) ? await FxStyle.LoadThemeAsync(assets, appearance.Style) : null;
/// var sheet = AppearanceCss.Compose(appearance, theme);
/// foreach (var error in sheet.Errors)
/// {
///     logger.LogWarning("{Param}: {Message}", error.Param, error.Message);
/// }
/// </code>
/// </example>
public static partial class AppearanceCss
{
    private const string Root = ":root:root:root";

    // The public variables and the tokens they set
    private static readonly Dictionary<string, string> s_tokens = new(StringComparer.Ordinal)
    {
        ["colorPrimary"] = "--fx-color-accent",
        ["colorBackground"] = "--fx-color-background",
        ["colorSurface"] = "--fx-color-surface",
        ["colorText"] = "--fx-color-text",
        ["colorTextSecondary"] = "--fx-color-muted",
        ["colorBorder"] = "--fx-color-border",
        ["colorDanger"] = "--fx-color-danger",
        ["colorWarning"] = "--fx-color-warning",
        ["colorSuccess"] = "--fx-color-success",
        ["colorInfo"] = "--fx-color-info",
        ["fontFamily"] = "--fx-font",
        ["borderRadius"] = "--fx-radius"
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
    /// Whether the appearance makes the Viewer its own (a style or a variable), so the base of a theme is needed: read it with
    /// <see cref="FxStyle.LoadThemeAsync"/> and pass it to <see cref="Compose"/>.
    /// </summary>
    /// <param name="appearance">The appearance; <see langword="null"/> is none.</param>
    /// <returns><see langword="true"/> when the tokens drive the Viewer.</returns>
    /// <example>
    /// <code>
    /// FxThemeSheets? theme = AppearanceCss.IsThemed(appearance) ? await FxStyle.LoadThemeAsync(assets, appearance!.Style) : null;
    /// </code>
    /// </example>
    public static bool IsThemed(ViewerAppearance? appearance)
    {
        return appearance is not null && (!string.IsNullOrWhiteSpace(appearance.Style) || appearance.Variables.Count > 0);
    }

    /// <summary>
    /// Makes the stylesheet of an appearance.
    /// </summary>
    /// <param name="appearance">The appearance; <see langword="null"/> is none.</param>
    /// <param name="theme">The base of a theme and the file of the style (<see cref="FxStyle.LoadThemeAsync"/>); needed when
    /// <see cref="IsThemed"/>, else the variables reach the content only and the style is left out.</param>
    /// <returns>The CSS and what was left out.</returns>
    /// <example>
    /// <code>
    /// var sheet = AppearanceCss.Compose(new ViewerAppearance { Variables = { ["colorPrimary"] = "#0f6cbd" } }, theme);
    /// </code>
    /// </example>
    public static AppearanceStylesheet Compose(ViewerAppearance? appearance, FxThemeSheets? theme = null)
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

        if (theme is not null && IsThemed(appearance))
        {
            css.Append(theme.Base.Trim()).Append('\n');
        }

        AppendStyle(css, appearance.Style, theme, errors);

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
            css.Append(CultureInfo.InvariantCulture, $"@layer {FxStyle.CustomLayer} {{\n").Append(rules).Append("}\n");
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

        return new AppearanceStylesheet(css.Length == 0 ? string.Empty : $"@layer {FxStyle.VendorLayer}, {FxStyle.CustomLayer};\n{css}", errors);
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

    // The file of the style passes the checks of a customer style: nothing from outside, no notices of the page styled away, no end of the style element
    private static void AppendStyle(StringBuilder css, string? style, FxThemeSheets? theme, List<ViewerConfigError> errors)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return;
        }

        if (!FxStyle.IsThemeName(style))
        {
            errors.Add(new ViewerConfigError("invalid_value", "appearance.style", $"'{style}' is not the name of a style: lowercase letters, digits and hyphens."));
            return;
        }

        if (theme is null)
        {
            return;
        }

        if (theme.Theme is null)
        {
            errors.Add(new ViewerConfigError("invalid_value", "appearance.style", $"There is no style '{style}': the library has no {FxStyle.ThemePathOf(style)}."));
            return;
        }

        var problem = theme.Theme.Contains("</", StringComparison.Ordinal) ? "The style may not contain '</'." : StyleChecks.Check(theme.Theme);
        if (problem is not null)
        {
            errors.Add(new ViewerConfigError("invalid_value", "appearance.style", $"The style '{style}' is left out: {problem}"));
            return;
        }

        css.Append(CultureInfo.InvariantCulture, $"@layer {FxStyle.CustomLayer} {{\n").Append(theme.Theme.Trim()).Append("\n}\n");
    }

    private static void AppendVariable(StringBuilder css, string name, string raw, List<ViewerConfigError> errors)
    {
        var param = $"appearance.variables.{name}";
        if (!s_tokens.TryGetValue(name, out var token))
        {
            errors.Add(new ViewerConfigError("unknown_option", param, $"'{name}' is not a variable of the appearance."));
            return;
        }

        var value = raw?.Trim() ?? string.Empty;
        var valid = name == "fontFamily" ? FontPattern().IsMatch(value) : ValuePattern().IsMatch(value) && !value.Contains("url", StringComparison.OrdinalIgnoreCase);
        if (!valid)
        {
            errors.Add(new ViewerConfigError("invalid_value", param,
                                             name == "fontFamily" ? "A font family is names separated by commas." : "The value is not a CSS colour or length."));
            return;
        }

        Declare(css, token, value);
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
