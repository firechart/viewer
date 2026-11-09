using System.Text;
using System.Text.RegularExpressions;

namespace FireChart.Viewer.Library;

/// <summary>
/// What a style must satisfy before it is used inside the layers of the vendor style.
/// </summary>
/// <remarks>
/// A style goes to every client inside a cascade layer: it must not leave the layer (the braces are balanced), must not ask anything from outside (a request
/// would tell a third party that the page of a patient was opened: no <c>url()</c>, no <c>@import</c>) and must not carry scripts.
/// </remarks>
/// <example>
/// <code>
/// if (StyleChecks.Check(customerCss) is { } error)
/// {
///     return BadRequest(error);
/// }
/// </code>
/// </example>
public static partial class StyleChecks
{
    /// <summary>
    /// The longest style accepted: 64 KB.
    /// </summary>
    public const int MaxLength = 64 * 1024;

    /// <summary>
    /// Checks the style of a customer: the checks of <see cref="CheckVendor"/>, and the classes of the safety net of the page may not be styled (a style
    /// could hide them).
    /// </summary>
    /// <param name="css">The style; <see langword="null"/> or empty is fine.</param>
    /// <returns>The first problem as a sentence, or <see langword="null"/> when the style is fine.</returns>
    public static string? Check(string? css)
    {
        return Validate(css, customer: true);
    }

    /// <summary>
    /// Checks the vendor style: the length, the balance of the braces, nothing from outside, no scripts.
    /// </summary>
    /// <param name="css">The style; <see langword="null"/> or empty is fine.</param>
    /// <returns>The first problem as a sentence, or <see langword="null"/> when the style is fine.</returns>
    public static string? CheckVendor(string? css)
    {
        return Validate(css, customer: false);
    }

    private static string? Validate(string? css, bool customer)
    {
        if (string.IsNullOrEmpty(css))
        {
            return null;
        }

        if (css.Length > MaxLength)
        {
            return $"The style is longer than {MaxLength / 1024} KB.";
        }

        var scan = Scan(css);
        if (scan.Error is not null)
        {
            return scan.Error;
        }

        var match = Forbidden().Match(scan.Code);
        if (match.Success)
        {
            return $"\"{match.Value.Trim()}\" is not allowed in the style.";
        }

        return customer && SafetyNetClass().Match(scan.Code) is { Success: true } net
            ? $"\"{net.Value}\" belongs to the notices of the page about data that is not shown and cannot be styled."
            : null;
    }

    // The code without the comments and the strings; an escape outside a string is an error
    private static ScanResult Scan(string css)
    {
        var code = new StringBuilder(css.Length);
        var depth = 0;
        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];
            if (c == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                var end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    return new ScanResult(string.Empty, "A comment is not closed.");
                }

                i = end + 1;
                code.Append(' ');
                continue;
            }

            if (c is '"' or '\'')
            {
                var j = i + 1;
                while (j < css.Length && css[j] != c && css[j] != '\n')
                {
                    j += css[j] == '\\' ? 2 : 1;
                }

                if (j >= css.Length || css[j] != c)
                {
                    return new ScanResult(string.Empty, "A string is not closed.");
                }

                // A string stays as a marker; one that starts with data: is the only address url() may have
                code.Append(css.AsSpan(i + 1).StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? "\"data:\"" : "\"\"");
                i = j;
                continue;
            }

            if (c == '\\')
            {
                return new ScanResult(string.Empty, "Escapes (\\) are not allowed outside strings.");
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}' && --depth < 0)
            {
                return new ScanResult(string.Empty, "A closing brace has no opening one.");
            }

            code.Append(c);
        }

        return depth == 0 ? new ScanResult(code.ToString(), null) : new ScanResult(string.Empty, "An opening brace is not closed.");
    }

    private sealed record ScanResult(string Code, string? Error);

    [GeneratedRegex("fx-(notice|other)[a-z0-9_-]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SafetyNetClass();

    // @import, url() to anything but data:, image-set(), expression(), scripts, bindings, markup
    [GeneratedRegex("""@import|url\s*\((?!\s*("data:|data:))|image-set\s*\(|expression\s*\(|javascript:|vbscript:|behavior\s*:|-moz-binding|<\s*/?\s*[a-z!]""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Forbidden();
}
