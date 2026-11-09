using System.Text.RegularExpressions;

namespace FireChart.Viewer.Core;

// The declarations of a template are read from its text, without rendering it (a template cannot be rendered before its data is there):
// they stand as string literals, one tag each
internal static partial class TemplateNeeds
{
    public static FhirNeeds Of(string text)
    {
        var include = new List<string>();
        var revInclude = new List<string>();
        foreach (Match match in Declaration().Matches(text))
        {
            (match.Groups[1].Value == "revinclude" ? revInclude : include).Add(match.Groups[2].Value.Trim());
        }

        return new FhirNeeds([.. include.Distinct(StringComparer.Ordinal)], [.. revInclude.Distinct(StringComparer.Ordinal)]);
    }

    [GeneratedRegex(@"\{%-?\s*fhir_(include|revinclude)\s+""([^""]+)""\s*-?%\}")]
    private static partial Regex Declaration();
}
