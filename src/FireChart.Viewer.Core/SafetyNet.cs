using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Hl7.Fhir.ElementModel;

namespace FireChart.Viewer.Core;

internal sealed record SafetyNetBlocks(string Top, string Bottom)
{
    public static readonly SafetyNetBlocks None = new(string.Empty, string.Empty);
}

// The page of a resource must not look complete when it is not. What the templates did not show is written by the engine itself, after the templates:
// the elements of modifierExtension (they change the meaning of the record) always, at the top; a notice with the count of the elements that are not shown, at the top;
// and these elements, as plain name and value, under "Other data" at the end. A template cannot switch it off.
// Limits: an element counts as shown when a template read it and did not give it to the generic _complex template (a template that reads a value and prints
// nothing is not noticed); only the elements of the resource and the extensions inside the shown elements are checked, not every element of a shown structure;
// id, meta, implicitRules, language and text (the narrative) are not data of the record for this purpose
internal static partial class SafetyNet
{
    private const int MaxNodes = 500;
    private const int MaxDepth = 6;
    private const int MaxTreeDepth = 24;
    private const int MaxNamesInNotice = 8;
    private const string ModifierExtension = "modifierExtension";
    private const string ExtensionName = "extension";

    private static readonly HashSet<string> s_skipped = new(StringComparer.Ordinal) { "id", "meta", "implicitRules", "language", "text" };

    public static SafetyNetBlocks Build(ITypedElement root, RenderTracker tracker)
    {
        var modifiers = new List<ITypedElement>();
        CollectModifiers(root, modifiers, 0);

        var unshown = new List<ITypedElement>();
        foreach (var child in root.Children())
        {
            if (s_skipped.Contains(child.Name) || child.Name == ModifierExtension)
            {
                continue;
            }

            if (tracker.IsShown(child.Location))
            {
                CollectUnshownExtensions(child, tracker, unshown, 0);
            }
            else
            {
                unshown.Add(child);
            }
        }

        if (modifiers.Count == 0 && unshown.Count == 0)
        {
            return SafetyNetBlocks.None;
        }

        var counter = new Counter(MaxNodes);
        var top = new StringBuilder();
        if (modifiers.Count > 0)
        {
            top.Append("<div class=\"fx-notice fx-notice-danger\"><div class=\"fx-notice-title\">Modifier extensions: they can change the meaning of this record</div>");
            foreach (var modifier in modifiers)
            {
                AppendField(top, modifier, 0, counter);
            }

            top.Append("</div>");
        }

        var bottom = new StringBuilder();
        if (unshown.Count > 0)
        {
            var names = unshown.Select(element => RelativePath(element.Location)).Distinct(StringComparer.Ordinal).ToList();
            var shownNames = string.Join(", ", names.Take(MaxNamesInNotice).Select(Encode)) + (names.Count > MaxNamesInNotice ? ", ..." : string.Empty);
            top.Append("<div class=\"fx-notice fx-notice-warning\">Not shown above: ")
               .Append(unshown.Count)
               .Append(unshown.Count == 1 ? " element (" : " elements (")
               .Append(shownNames)
               .Append("). They are listed under \"Other data\" at the end of the page.</div>");

            bottom.Append("<details class=\"fx-other\"><summary>Other data (").Append(unshown.Count).Append(")</summary>");
            foreach (var element in unshown)
            {
                AppendField(bottom, element, 0, counter);
            }

            bottom.Append("</details>");
        }

        return new SafetyNetBlocks(top.ToString(), bottom.ToString());
    }

    // Shown when the check itself failed: silence would be the very thing the net is there to prevent
    public static SafetyNetBlocks Failed()
    {
        return new SafetyNetBlocks("<div class=\"fx-notice fx-notice-warning\">The check for data that is not shown could not be completed: do not take this page for complete.</div>", string.Empty);
    }

    private static void CollectModifiers(ITypedElement node, List<ITypedElement> found, int depth)
    {
        if (depth > MaxTreeDepth)
        {
            return;
        }

        foreach (var child in node.Children())
        {
            if (child.Name == ModifierExtension)
            {
                found.Add(child);
            }
            else
            {
                CollectModifiers(child, found, depth + 1);
            }
        }
    }

    private static void CollectUnshownExtensions(ITypedElement node, RenderTracker tracker, List<ITypedElement> found, int depth)
    {
        if (depth > MaxTreeDepth)
        {
            return;
        }

        foreach (var child in node.Children())
        {
            if (child.Name == ModifierExtension)
            {
                continue;
            }

            if (tracker.IsShown(child.Location))
            {
                CollectUnshownExtensions(child, tracker, found, depth + 1);
            }
            else if (child.Name == ExtensionName)
            {
                found.Add(child);
            }
        }
    }

    private static void AppendField(StringBuilder output, ITypedElement element, int depth, Counter counter)
    {
        var url = IsExtension(element) ? UrlOf(element) : null;
        output.Append("<div class=\"fx-field\"><div class=\"fx-label\"");
        if (url is not null)
        {
            output.Append(" title=\"").Append(Encode(url)).Append('"');
        }

        output.Append('>').Append(Encode(url is null ? (depth == 0 ? RelativePath(element.Location) : element.Name) : LastSegment(url))).Append("</div><div class=\"fx-value\">");
        AppendValue(output, element, depth, counter);
        output.Append("</div></div>");
    }

    private static void AppendValue(StringBuilder output, ITypedElement element, int depth, Counter counter)
    {
        if (!counter.Take())
        {
            output.Append("...");
            return;
        }

        if (element.Value is not null)
        {
            output.Append(Encode(new FhirNode(element).ValueText() ?? string.Empty));
            return;
        }

        if (depth >= MaxDepth)
        {
            output.Append("...");
            return;
        }

        var extension = IsExtension(element);
        foreach (var child in element.Children())
        {
            if (extension && child.Name == "url")
            {
                continue;
            }

            if (extension && child.Name.StartsWith("value", StringComparison.Ordinal))
            {
                AppendValue(output, child, depth + 1, counter);
            }
            else
            {
                AppendField(output, child, depth + 1, counter);
            }
        }
    }

    private static bool IsExtension(ITypedElement element)
    {
        return element.Name is ExtensionName or ModifierExtension;
    }

    private static string? UrlOf(ITypedElement element)
    {
        return element.Children("url").FirstOrDefault()?.Value as string;
    }

    private static string LastSegment(string url)
    {
        var cut = url.LastIndexOfAny(['/', '#']);
        return cut >= 0 && cut < url.Length - 1 ? url[(cut + 1)..] : url;
    }

    // Patient.contact[0].extension -> contact.extension
    private static string RelativePath(string location)
    {
        var path = Index().Replace(location, string.Empty);
        var dot = path.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? path : path[(dot + 1)..];
    }

    private static string Encode(string text)
    {
        return HtmlEncoder.Default.Encode(text);
    }

    [GeneratedRegex(@"\[\d+\]", RegexOptions.CultureInvariant)]
    private static partial Regex Index();

    private sealed class Counter(int budget)
    {
        private int _left = budget;

        public bool Take()
        {
            return _left-- > 0;
        }
    }
}
