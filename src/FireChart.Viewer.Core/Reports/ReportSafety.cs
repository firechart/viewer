using System.Globalization;
using System.Text.RegularExpressions;
using Hl7.Fhir.Model;

namespace FireChart.Viewer.Core.Reports;

// The page of a record must not look complete when it is not (the safety net of the templates, kept for the components). What the component of the
// view does not show is said by the page itself, which no component can switch off: the modifierExtension of the record at any depth (they change its
// meaning) always, in red at the top; the count and the names of the elements of the record the view does not show, at the top; and these elements as
// plain name and value under "Other data" at the end.
// An element counts as shown when an expression of the view (or of a component it gives %record to) names it: %record.code shows code. A view that
// names an element and prints nothing is not noticed; the built-in view shows every element. id, meta, implicitRules, language and text (the
// narrative) are not data of the record for this purpose
internal static partial class ReportSafety
{
    private const int MaxTreeDepth = 24;
    private const int MaxNamesInNotice = 8;
    private const string ModifierExtension = "modifierExtension";

    private static readonly HashSet<string> s_skipped = new(StringComparer.Ordinal) { "id", "meta", "implicitRules", "language", "text", "contained" };

    // The elements of %record a view names, with the components it calls with %record (under the name of their parameter)
    public static HashSet<string> ShownBy(ComponentDefinition view, IReadOnlyDictionary<string, ComponentDefinition> components)
    {
        var shown = new HashSet<string>(StringComparer.Ordinal);
        Collect(view, ReportVariables.Record, components, shown, []);
        return shown;
    }

    private static void Collect(ComponentDefinition component, string variable, IReadOnlyDictionary<string, ComponentDefinition> components, HashSet<string> shown,
                                HashSet<string> seen)
    {
        if (!seen.Add($"{component.Name}|{variable}"))
        {
            return;
        }

        var pattern = new Regex($@"%{Regex.Escape(variable)}\.([A-Za-z][A-Za-z0-9]*)", RegexOptions.CultureInvariant);
        foreach (var value in component.Data.Select(search => search.Query).Concat(Values(component.Body)))
        {
            foreach (var expression in value.Expressions)
            {
                foreach (Match match in pattern.Matches(expression.Source))
                {
                    shown.Add(match.Groups[1].Value);
                }
            }
        }

        // A component called with %record itself (record="{{%record}}") shows what it names of its parameter
        foreach (var call in Calls(component.Body))
        {
            if (!components.TryGetValue(call.Name, out var called))
            {
                continue;
            }

            foreach (var (name, value) in call.Attributes)
            {
                if (value.Single is { Source: var source } && source == "%" + variable)
                {
                    Collect(called, name, components, shown, seen);
                }
            }
        }
    }

    private static IEnumerable<ReportValue> Values(IEnumerable<ReportNode> nodes)
    {
        foreach (var node in nodes)
        {
            foreach (var value in node.Attributes.Values)
            {
                yield return value;
            }

            foreach (var value in Values(node.Children))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<ReportNode> Calls(IEnumerable<ReportNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Name != ReportNode.TextName && !ReportSchema.BuiltIns.ContainsKey(node.Name) && node.Name != ReportSchema.Fill)
            {
                yield return node;
            }

            foreach (var call in Calls(node.Children))
            {
                yield return call;
            }
        }
    }

    // What the page says of a record a view shows: shown null is the built-in view, which shows every element
    public static SafetyReport Check(PocoNode record, IReadOnlySet<string>? shown, ReportFormat culture)
    {
        var modifiers = new List<SafetyField>();
        CollectModifiers(record, modifiers, culture, 0);
        var unshown = shown is null
            ? []
            : ReportText.All(record).Where(child => !s_skipped.Contains(child.Name) && child.Name != ModifierExtension && !shown.Contains(child.Name))
                        .GroupBy(child => child.Name)
                        .Select(group => new SafetyField(group.Key, string.Join(", ", group.Select(child => ReportText.Of(child, culture)).Where(text => text.Length > 0)), null))
                        .ToList();
        return new SafetyReport(modifiers, unshown);
    }

    private static void CollectModifiers(PocoNode node, List<SafetyField> found, ReportFormat culture, int depth)
    {
        if (depth > MaxTreeDepth)
        {
            return;
        }

        foreach (var child in ReportText.All(node))
        {
            if (child.Name == ModifierExtension)
            {
                var url = ReportText.Text(child, "url", culture);
                var name = url.Split('/', '#')[^1];
                var value = ReportText.Children(child, "value").FirstOrDefault() is { } given ? ReportText.Of(given, culture) : string.Empty;
                found.Add(new SafetyField(name.Length > 0 ? name : ModifierExtension, value, url));
            }
            else if (child.Poco is not PrimitiveType)
            {
                CollectModifiers(child, found, culture, depth + 1);
            }
        }
    }

    // The names of the notice: at most a few, then "..."
    public static string Names(IReadOnlyList<SafetyField> fields)
    {
        return string.Join(", ", fields.Take(MaxNamesInNotice).Select(field => field.Name)) + (fields.Count > MaxNamesInNotice ? ", ..." : string.Empty);
    }
}

// An element the page reports: its name (the last segment of the url of a modifier extension, whose url is the title), its value as text
internal sealed record SafetyField(string Name, string Text, string? Title);

internal sealed record SafetyReport(IReadOnlyList<SafetyField> Modifiers, IReadOnlyList<SafetyField> Unshown);
