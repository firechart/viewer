using System.Text.RegularExpressions;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Fhir.Metadata;

namespace FireChart.Viewer.Core.Reports;

// What a search must bring with its records so their tiles read nothing more: the records a tile names in its patient and encounter (_include of
// the reference parameter of that element, %record.subject is Encounter:subject)
internal static partial class ReportNeeds
{
    private static readonly string[] s_releases = ["R4", "R5", "R4B", "STU3"];

    public static FhirNeeds? OfTile(ComponentDefinition? tile, string type, params IEnumerable<string> more)
    {
        var include = new List<string>();
        foreach (var attribute in new[] { "patient", "encounter" })
        {
            if (tile is not null && Find(tile.Body, ReportSchema.Tile, attribute) is { } value && IncludeOf(type, value) is { } found)
            {
                include.Add(found);
            }
        }

        include.AddRange(more);
        return include.Count > 0 ? new FhirNeeds([.. include.Distinct(StringComparer.Ordinal)], []) : null;
    }

    // The needs of the tile of a type, read from the library
    public static async Task<FhirNeeds?> OfTileAsync(ReportLibrary library, string type, CancellationToken cancellationToken = default)
    {
        var bundle = await library.LoadAsync($"{type}.tile", cancellationToken);
        return OfTile(bundle.Problems.Length == 0 ? bundle.Root : null, type);
    }

    // The _include of an element of the record (%record.subject): the reference parameter of the type whose path is that element
    private static string? IncludeOf(string type, ReportValue value)
    {
        if (value.Single?.Source is not { } source || RecordElement().Match(source.Trim()) is not { Success: true } match)
        {
            return null;
        }

        var path = $"{type}.{match.Groups[1].Value}";
        foreach (var release in s_releases)
        {
            if (FhirMetadata.Of(release)?.ParametersOf(type).FirstOrDefault(p => p.Type == Forms.SearchParameterType.Reference && p.Path == path) is { } parameter)
            {
                return $"{type}:{parameter.Code}";
            }
        }

        return null;
    }

    // The value of the attribute of the first block of the name among the nodes
    private static ReportValue? Find(IEnumerable<ReportNode> nodes, string name, string attribute)
    {
        foreach (var node in nodes)
        {
            if (node.Name == name && node.Attribute(attribute) is { } value)
            {
                return value;
            }

            if (Find(node.Children, name, attribute) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^%record\.([A-Za-z]+)$")]
    private static partial Regex RecordElement();
}
