using System.Net;
using System.Text.RegularExpressions;

namespace FireChart.Viewer;

// The HTML of a template as one line of text: the title of a bookmark
internal static partial class ViewerText
{
    // The mode of the templates that renders a resource as a line of text
    public const string Mode = "text";

    private const int MaxLength = 160;

    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();

    public static string? Plain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = Spaces().Replace(WebUtility.HtmlDecode(Tag().Replace(html, " ")), " ").Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= MaxLength ? text : text[..(MaxLength - 1)].TrimEnd() + "…";
    }
}
