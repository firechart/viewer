using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FireChart.Viewer.Core.Reports;

// Reads the XML of a component: <Component name> with <Param>, <Slot>, <Data><Search/></Data> and its body. Strict: malformed XML, a misplaced
// declaration or an expression left open is an exception with its line; whether the body is made of known components is ReportCheck's job
internal static partial class ReportXml
{
    public const string ComponentElement = "Component";
    public const string ParamElement = "Param";
    public const string SlotElement = "Slot";
    public const string DataElement = "Data";
    public const string SearchElement = "Search";

    public const int DefaultMax = 50;

    public static ComponentDefinition Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"{ex.LineNumber}: {ex.Message}");
        }

        var root = document.Root!;
        if (root.Name.LocalName != ComponentElement || root.Name.Namespace != XNamespace.None)
        {
            throw Error(root, $"the root is <{ComponentElement} name=\"…\">, not <{root.Name.LocalName}>");
        }

        var name = Required(root, "name");
        if (!NameRegex().IsMatch(name))
        {
            throw Error(root, $"\"{name}\" is not a name of a component: a letter, then letters, digits, '.' or '-'");
        }

        var parameters = ImmutableArray.CreateBuilder<ParamDefinition>();
        var slots = ImmutableArray.CreateBuilder<string>();
        var data = ImmutableArray.CreateBuilder<SearchDefinition>();
        var body = ImmutableArray.CreateBuilder<ReportNode>();
        foreach (var node in root.Nodes())
        {
            switch (node)
            {
                case XElement { Name.LocalName: ParamElement } param:
                    parameters.Add(new ParamDefinition(Required(param, "name"), Optional(param, "required") == "true", Line(param)));
                    break;
                case XElement { Name.LocalName: SlotElement } slot:
                    slots.Add(Optional(slot, "name") ?? string.Empty);
                    break;
                case XElement { Name.LocalName: DataElement } dataElement:
                    data.AddRange(dataElement.Elements().Select(ReadSearch));
                    break;
                default:
                    if (ReadNode(node) is { } read)
                    {
                        body.Add(read);
                    }

                    break;
            }
        }

        return new ComponentDefinition(name, parameters.ToImmutable(), slots.ToImmutable(), data.ToImmutable(), body.ToImmutable(), Line(root));
    }

    // A value with {{ }} parts; a text outside them is literal
    public static ReportValue ParseValue(string source, int line = 0)
    {
        var parts = ImmutableArray.CreateBuilder<ReportPart>();
        var position = 0;
        while (position < source.Length)
        {
            var open = source.IndexOf("{{", position, StringComparison.Ordinal);
            if (open < 0)
            {
                parts.Add(new ReportLiteral(source[position..]));
                break;
            }

            if (open > position)
            {
                parts.Add(new ReportLiteral(source[position..open]));
            }

            var close = source.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                throw new InvalidDataException($"{line}: '{{{{' without '}}}}' in \"{source}\"");
            }

            var expression = source[(open + 2)..close].Trim();
            if (expression.Length == 0)
            {
                throw new InvalidDataException($"{line}: an empty expression in \"{source}\"");
            }

            parts.Add(StringRegex().Match(expression) is { Success: true } text
                ? new ReportString(text.Groups[1].Value, [.. Arguments(text.Groups[2].Value, line, source).Select(ExpressionOf)])
                : ExpressionOf(expression));
            position = close + 2;
        }

        return new ReportValue(source, parts.ToImmutable());
    }

    private static ReportExpression ExpressionOf(string source)
    {
        return new ReportExpression(source, [.. VariableRegex().Matches(source).Select(match => match.Groups[1].Value).Distinct()]);
    }

    // The arguments of a text, split at the commas outside parentheses and quotes
    private static IEnumerable<string> Arguments(string list, int line, string source)
    {
        if (list.Trim().Length == 0)
        {
            yield break;
        }

        var depth = 0;
        var quoted = false;
        var start = 0;
        for (var i = 0; i < list.Length; i++)
        {
            switch (list[i])
            {
                case '\'' when i == 0 || list[i - 1] != '\\':
                    quoted = !quoted;
                    break;
                case '(' when !quoted:
                    depth++;
                    break;
                case ')' when !quoted:
                    depth--;
                    break;
                case ',' when !quoted && depth == 0:
                    yield return Argument(list[start..i], line, source);
                    start = i + 1;
                    break;
            }
        }

        yield return Argument(list[start..], line, source);
    }

    private static string Argument(string text, int line, string source)
    {
        return text.Trim() is { Length: > 0 } argument ? argument : throw new InvalidDataException($"{line}: an empty argument in \"{source}\"");
    }

    private static SearchDefinition ReadSearch(XElement element)
    {
        if (element.Name.LocalName != SearchElement)
        {
            throw Error(element, $"<{DataElement}> holds only <{SearchElement} id=\"…\" query=\"…\"/>");
        }

        var max = Optional(element, "max") is { } text ? int.TryParse(text, out var value) && value is > 0 and <= 1000
                                                             ? value
                                                             : throw Error(element, "max is a number from 1 to 1000")
                                                       : DefaultMax;
        return new SearchDefinition(Required(element, "id"), ParseValue(Required(element, "query"), Line(element)), max, Line(element));
    }

    private static ReportNode? ReadNode(XNode node)
    {
        switch (node)
        {
            case XElement element:
                if (element.Name.LocalName is ParamElement or SlotElement or DataElement or SearchElement or ComponentElement)
                {
                    throw Error(element, $"<{element.Name.LocalName}> belongs at the top of the component");
                }

                var attributes = element.Attributes()
                                        .Where(attribute => !attribute.IsNamespaceDeclaration)
                                        .ToImmutableDictionary(attribute => attribute.Name.LocalName, attribute => ParseValue(attribute.Value, Line(element)));
                var children = element.Nodes().Select(ReadNode).OfType<ReportNode>().ToImmutableArray();
                return new ReportNode(element.Name.LocalName, attributes, children, Line(element));
            case XText text:
                var normalized = Normalize(text.Value);

                // A space on the same line as an element beside it is text ("ID <Value/>"); a line break is the layout of the XML
                if (text.PreviousNode is XElement && text.Value.Length > 0 && char.IsWhiteSpace(text.Value[0]) && !LeadingBreak(text.Value))
                {
                    normalized = " " + normalized;
                }

                if (text.NextNode is XElement && normalized.Trim().Length > 0 && char.IsWhiteSpace(text.Value[^1]) && !TrailingBreak(text.Value))
                {
                    normalized += " ";
                }

                return normalized.Length == 0
                    ? null
                    : new ReportNode(ReportNode.TextName, ImmutableDictionary<string, ReportValue>.Empty.Add(ReportNode.TextName, ParseValue(normalized, Line(text))), [], Line(text));
            default:
                return null;
        }
    }

    private static bool LeadingBreak(string text)
    {
        return text[..(text.Length - text.TrimStart().Length)].Contains('\n', StringComparison.Ordinal);
    }

    private static bool TrailingBreak(string text)
    {
        return text[text.TrimEnd().Length..].Contains('\n', StringComparison.Ordinal);
    }

    // The whitespace of the layout of the XML is not text: runs become one space, a text of whitespace alone is dropped
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space && builder.Length > 0)
            {
                builder.Append(' ');
            }

            space = false;
            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string Required(XElement element, string name)
    {
        return Optional(element, name) is { Length: > 0 } value ? value : throw Error(element, $"<{element.Name.LocalName}> needs {name}=\"…\"");
    }

    private static string? Optional(XElement element, string name)
    {
        return element.Attribute(name)?.Value;
    }

    private static int Line(XObject node)
    {
        return ((IXmlLineInfo)node).LineNumber;
    }

    private static InvalidDataException Error(XObject node, string message)
    {
        return new InvalidDataException($"{Line(node)}: {message}");
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9.-]*$")]
    private static partial Regex NameRegex();

    [GeneratedRegex("%([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex VariableRegex();

    // %strings.Key or %strings.Key(arguments)
    [GeneratedRegex(@"^%strings\.([A-Za-z_][A-Za-z0-9_]*)(?:\((.*)\))?$", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex StringRegex();
}
