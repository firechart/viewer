using System.Text.Json;

namespace FireChart.Viewer.Pages;

/// <summary>
/// A group of the laboratory results of a section: the results of one report (a DiagnosticReport, in the order the report lists them: the order of the
/// panel the laboratory gives), or the results of one day that no loaded report lists.
/// </summary>
/// <param name="Key">The key of the group on the page.</param>
/// <param name="Report">The report, or <see langword="null"/> for a group of a day.</param>
/// <param name="Day">The day of a group of a day, in the zone of the display; <see langword="null"/> for a report and for results without a moment.</param>
/// <param name="Rows">The results (Observation).</param>
public sealed record LabGroup(string Key, ResourceSummary? Report, DateOnly? Day, IReadOnlyList<ResourceSummary> Rows);

/// <summary>
/// The grouping of the component <c>lab-results</c>: Observations under the DiagnosticReports that list them, the rest by day, newest first.
/// </summary>
/// <example>
/// <code>
/// var groups = LabResults.Group(section.Items, includedObservations, TimeZoneInfo.Local);
/// </code>
/// </example>
public static class LabResults
{
    /// <summary>The template of the cells of a row (slots test, value, range, flag): the partial <c>lab-row</c>.</summary>
    public const string RowTemplate = "lab-row";

    /// <summary>What a search of reports of the component asks to come with them: their results.</summary>
    public const string ReportResults = "DiagnosticReport:result";

    private const string Observation = "Observation";
    private const string DiagnosticReport = "DiagnosticReport";

    /// <summary>
    /// Groups the records of a section.
    /// </summary>
    /// <param name="items">The records of the section (reports and results), newest first.</param>
    /// <param name="included">Results that came with the reports (<c>_include</c>) and are not records of the section.</param>
    /// <param name="zone">The zone of the display: the day of a result.</param>
    /// <returns>The groups, newest first; a report keeps its place even when none of its results came.</returns>
    /// <example>
    /// <code>
    /// var groups = LabResults.Group(items, [], TimeZoneInfo.Utc);
    /// </code>
    /// </example>
    public static IReadOnlyList<LabGroup> Group(IReadOnlyList<ResourceSummary> items, IEnumerable<ResourceSummary> included, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(included);
        ArgumentNullException.ThrowIfNull(zone);
        var results = new Dictionary<string, ResourceSummary>(StringComparer.Ordinal);
        foreach (var result in items.Where(item => item.ResourceType == Observation).Concat(included.Where(item => item.ResourceType == Observation)))
        {
            results.TryAdd($"{Observation}/{result.Id}", result);
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<(LabGroup Group, DateTimeOffset? Moment)>();
        foreach (var report in items.Where(item => item.ResourceType == DiagnosticReport))
        {
            var rows = new List<ResourceSummary>();
            foreach (var reference in ResultsOf(report.Json))
            {
                if (ResourcePool.KeyOf(reference) is { } key && results.TryGetValue(key, out var result) && used.Add(key))
                {
                    rows.Add(result);
                }
            }

            groups.Add((new LabGroup($"{DiagnosticReport}/{report.Id}", report, null, rows), ResourceMoment.Of(report.ResourceType, report.Json)));
        }

        foreach (var day in items.Where(item => item.ResourceType == Observation && !used.Contains($"{Observation}/{item.Id}"))
                                 .Select(item => (Item: item, Moment: ResourceMoment.Of(item.ResourceType, item.Json)))
                                 .GroupBy(entry => entry.Moment is { } moment ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime) : (DateOnly?)null))
        {
            var key = day.Key is { } date ? "day:" + date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "day:none";
            groups.Add((new LabGroup(key, null, day.Key, [.. day.Select(entry => entry.Item)]), day.Max(entry => entry.Moment)));
        }

        return [.. groups.Select((entry, index) => (entry, index))
                         .OrderByDescending(pair => pair.entry.Moment.HasValue)
                         .ThenByDescending(pair => pair.entry.Moment)
                         .ThenBy(pair => pair.index)
                         .Select(pair => pair.entry.Group)];
    }

    /// <summary>
    /// The records of results from the resources that came with the reports.
    /// </summary>
    /// <param name="related">What the searches brought with their records.</param>
    /// <returns>The Observations among them.</returns>
    /// <example>
    /// <code>
    /// var included = LabResults.ObservationsOf(page.Related ?? []);
    /// </code>
    /// </example>
    public static IReadOnlyList<ResourceSummary> ObservationsOf(IEnumerable<RelatedResource> related)
    {
        ArgumentNullException.ThrowIfNull(related);
        var result = new List<ResourceSummary>();
        foreach (var resource in related)
        {
            try
            {
                using var document = JsonDocument.Parse(resource.Json);
                var root = document.RootElement;
                if (root.TryGetProperty("resourceType", out var type) && type.GetString() == Observation && root.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } text)
                {
                    result.Add(new ResourceSummary(text, Observation, resource.Json));
                }
            }
            catch (JsonException)
            {
                // not a resource: nothing to group
            }
        }

        return result;
    }

    private static List<string> ResultsOf(string? json)
    {
        var references = new List<string>();
        if (json is null)
        {
            return references;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("result", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                foreach (var result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("reference", out var reference) && reference.GetString() is { Length: > 0 } text)
                    {
                        references.Add(text);
                    }
                }
            }
        }
        catch (JsonException)
        {
            references.Clear();
        }

        return references;
    }
}
