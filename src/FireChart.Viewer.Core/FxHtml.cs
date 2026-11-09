using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace FireChart.Viewer.Core;

/// <summary>
/// What a page lets through of the HTML a template wrote.
/// </summary>
/// <remarks>
/// The HTML is parsed as a browser would parse it and written again from the tree by a white list of tags and attributes; the rest is dropped. The values
/// of a FHIR server come into that HTML, so it is not trusted: no scripts, no styles of its own (a style may reach out), no images, no forms, links only to
/// resources (<c>fhir:Type/id</c>, which the caller turns into an address of the application) or to <c>https</c>.
/// </remarks>
/// <example>
/// <code>
/// var html = await renderer.RenderViewAsync(json, "view");
/// var safe = FxHtml.Sanitize(html, link =&gt; "/server/1/" + link[FxHtml.FhirScheme.Length..]);
/// </code>
/// </example>
public static partial class FxHtml
{
    /// <summary>
    /// The scheme of a link to a resource of the FHIR server in the HTML of a template: <c>fhir:Patient/p1</c>.
    /// </summary>
    public const string FhirScheme = "fhir:";

    private const int MaxDepth = 128;
    private const int MaxSpan = 20;
    private const string HtmlNamespace = "http://www.w3.org/1999/xhtml";

    private static readonly HashSet<string> s_tags = new(StringComparer.Ordinal)
    {
        "div", "span", "section", "article", "header", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "p", "a", "ul", "ol", "li", "dl", "dt", "dd",
        "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "details", "summary", "strong", "em", "b", "i", "small", "br", "hr", "time", "abbr", "mark"
    };

    // The row of a list is a link itself: its blocks and inline elements, no links of its own
    private static readonly HashSet<string> s_tileTags = new(StringComparer.Ordinal)
    {
        "li", "div", "p", "span", "strong", "em", "b", "i", "small", "sup", "sub", "mark", "abbr", "time", "br"
    };

    private static readonly HashSet<string> s_blocks = new(StringComparer.Ordinal) { "li", "div", "p", "br", "span" };

    private static readonly HashSet<string> s_void = new(StringComparer.Ordinal) { "br", "hr" };

    // Dropped with everything inside
    private static readonly HashSet<string> s_dropped = new(StringComparer.Ordinal)
    {
        "script", "style", "iframe", "object", "embed", "noscript", "template", "textarea", "title", "svg", "math", "select", "option", "button"
    };

    /// <summary>
    /// Sanitizes the HTML of the page of a resource.
    /// </summary>
    /// <param name="html">The HTML a template wrote; <see langword="null"/> gives an empty string.</param>
    /// <param name="resolveLink">The address of the application for a link <c>fhir:Type/id</c>; when it is <see langword="null"/>, or answers
    /// <see langword="null"/>, the link is dropped and its text stays.</param>
    /// <returns>The HTML that may be placed in the page.</returns>
    public static string Sanitize(string? html, Func<string, string?>? resolveLink = null)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var output = new StringBuilder(html.Length);
        foreach (var node in Fragment(html))
        {
            Write(output, node, 0, resolveLink, s_tags);
        }

        return output.ToString();
    }

    /// <summary>
    /// Sanitizes the HTML of a row of a list: the white list of <see cref="Sanitize"/> cut to what a row may have.
    /// </summary>
    /// <param name="html">The HTML a template of the mode <see cref="TileModel.Mode"/> wrote.</param>
    /// <returns>The HTML of the row: a link, a table, a heading are not let through, their text stays (the whole row is the link).</returns>
    public static string SanitizeTile(string? html)
    {
        return SanitizeTileWithText(html).Html;
    }

    // The row and its text from one parse: a list renders dozens of rows, and the parser is the most of their cost
    internal static SanitizedTile SanitizeTileWithText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return new SanitizedTile(string.Empty, string.Empty);
        }

        var output = new StringBuilder(html.Length);
        var text = new StringBuilder();
        foreach (var node in Fragment(html))
        {
            Write(output, node, 0, null, s_tileTags, text);
        }

        return new SanitizedTile(output.ToString(), WhiteSpace().Replace(text.ToString(), " ").Trim());
    }

    [ThreadStatic]
    private static HtmlParser? t_parser;

    [ThreadStatic]
    private static IElement? t_context;

    private static INodeList Fragment(string html)
    {
        t_parser ??= new HtmlParser();
        t_context ??= t_parser.ParseDocument(string.Empty).Body!;
        return t_parser.ParseFragment(html, t_context);
    }

    /// <summary>
    /// The text of a piece of HTML: entities decoded, tags dropped.
    /// </summary>
    /// <param name="html">A piece of HTML.</param>
    /// <returns>The text: what a tooltip or a search has of a line that has markup.</returns>
    /// <example>
    /// <code>
    /// FxHtml.PlainText("&lt;b&gt;Smith&lt;/b&gt; &amp;amp; Co");   // "Smith &amp; Co"
    /// </code>
    /// </example>
    public static string PlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var node in Fragment(html))
        {
            AppendText(text, node);
        }

        return WhiteSpace().Replace(text.ToString(), " ").Trim();
    }

    // A block ends with a space, so the lines of a row do not run into each other
    private static void AppendText(StringBuilder text, INode node)
    {
        if (node is IText data)
        {
            text.Append(data.Data);
            return;
        }

        if (node is not IElement element)
        {
            return;
        }

        var block = s_blocks.Contains(element.LocalName);
        if (block)
        {
            text.Append(' ');
        }

        foreach (var child in element.ChildNodes)
        {
            AppendText(text, child);
        }

        if (block)
        {
            text.Append(' ');
        }
    }

    // text: when given, the text of what is written, with a space around the blocks (as PlainText of the output)
    private static void Write(StringBuilder output, INode node, int level, Func<string, string?>? resolveLink, HashSet<string> tags, StringBuilder? text = null)
    {
        if (node is IText data)
        {
            Encode(output, data.Data, false);
            text?.Append(data.Data);
            return;
        }

        if (node is not IElement element || level >= MaxDepth || element.NamespaceUri != HtmlNamespace)
        {
            return;
        }

        var name = element.LocalName;
        if (s_dropped.Contains(name))
        {
            return;
        }

        if (!tags.Contains(name))
        {
            WriteChildren(output, element, level + 1, resolveLink, tags, text);
            return;
        }

        var block = text is not null && s_blocks.Contains(name);
        if (block)
        {
            text!.Append(' ');
        }

        Open(output, element, resolveLink);
        if (!s_void.Contains(name))
        {
            WriteChildren(output, element, level + 1, resolveLink, tags, text);
            output.Append("</").Append(name).Append('>');
        }

        if (block)
        {
            text!.Append(' ');
        }
    }

    private static void WriteChildren(StringBuilder output, IElement element, int level, Func<string, string?>? resolveLink, HashSet<string> tags, StringBuilder? text = null)
    {
        foreach (var child in element.ChildNodes)
        {
            Write(output, child, level, resolveLink, tags, text);
        }
    }

    private static void Open(StringBuilder output, IElement element, Func<string, string?>? resolveLink)
    {
        var tag = element.LocalName;
        output.Append('<').Append(tag);
        var external = false;
        foreach (var attribute in element.Attributes)
        {
            var value = attribute.Value;
            switch (attribute.Name)
            {
                case "class":
                    if (Classes(value) is { Length: > 0 } classes)
                    {
                        Attribute(output, "class", classes);
                    }

                    break;
                case "title" or "aria-label":
                    Attribute(output, attribute.Name, value);
                    break;
                case "href" when tag == "a":
                    if (Link(value, resolveLink, out var address, out external))
                    {
                        Attribute(output, "href", address);
                    }

                    break;
                case "open" when tag == "details":
                    output.Append(" open");
                    break;
                case "colspan" or "rowspan" when tag is "td" or "th":
                    if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var span) && span is > 0 and <= MaxSpan)
                    {
                        Attribute(output, attribute.Name, span.ToString(CultureInfo.InvariantCulture));
                    }

                    break;
                case "datetime" when tag == "time" && DateTimeText().IsMatch(value):
                    Attribute(output, "datetime", value);
                    break;
            }
        }

        if (external)
        {
            output.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
        }

        output.Append('>');
    }

    private static void Attribute(StringBuilder output, string name, string value)
    {
        output.Append(' ').Append(name).Append("=\"");
        Encode(output, value, true);
        output.Append('"');
    }

    private static void Encode(StringBuilder output, string value, bool attribute)
    {
        foreach (var c in value)
        {
            switch (c)
            {
                case '&':
                    output.Append("&amp;");
                    break;
                case '<':
                    output.Append("&lt;");
                    break;
                case '>':
                    output.Append("&gt;");
                    break;
                case '"' when attribute:
                    output.Append("&quot;");
                    break;
                default:
                    output.Append(c);
                    break;
            }
        }
    }

    // Only the classes of the kit: the style of the page does not know the others, and a class must not pick up the styles of the application
    private static string Classes(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Where(name => KitClass().IsMatch(name)));
    }

    private static bool Link(string value, Func<string, string?>? resolveLink, out string address, out bool external)
    {
        address = string.Empty;
        external = false;
        var text = value.Trim();
        if (text.StartsWith(FhirScheme, StringComparison.OrdinalIgnoreCase))
        {
            if (resolveLink is null || !ResourceLink().IsMatch(text))
            {
                return false;
            }

            address = resolveLink(FhirScheme + text[FhirScheme.Length..]) ?? string.Empty;
            return address.Length > 0;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            address = uri.AbsoluteUri;
            external = true;
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhiteSpace();

    [GeneratedRegex("^fx-[a-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex KitClass();

    [GeneratedRegex("^fhir:[A-Za-z]{1,64}/[A-Za-z0-9.-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ResourceLink();

    [GeneratedRegex(@"^[0-9TZ:+.\-]{4,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex DateTimeText();
}
