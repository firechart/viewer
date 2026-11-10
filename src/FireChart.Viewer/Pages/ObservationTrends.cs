using System.Globalization;
using System.Text.Json;

namespace FireChart.Viewer.Pages;

/// <summary>A point of a trend: the moment in the zone of the display and the value.</summary>
/// <param name="Moment">The moment, local to the display.</param>
/// <param name="Value">The value.</param>
public sealed record TrendPoint(DateTime Moment, double Value);

/// <summary>A line of a trend: the value of the observation, or of one of its components (blood pressure: systolic and diastolic).</summary>
/// <param name="Name">The name of the line.</param>
/// <param name="Unit">The unit of the points; the points of another unit are left out.</param>
/// <param name="Points">The points, oldest first.</param>
public sealed record TrendSeries(string Name, string? Unit, IReadOnlyList<TrendPoint> Points);

/// <summary>The records of one measure (one code) of a section, newest first.</summary>
/// <param name="Key">The key of the measure: the first coding <c>system|code</c>, else the text of the code.</param>
/// <param name="Search">The token of a search of its history (<c>system|code</c>, or a bare code); <see langword="null"/> when the code has none.</param>
/// <param name="Records">Its records in the section, newest first.</param>
public sealed record TrendGroup(string Key, string? Search, IReadOnlyList<ResourceSummary> Records);

/// <summary>
/// The data of the component <c>observation-trends</c>: the measures of a section and the lines of a chart of one of them.
/// </summary>
/// <example>
/// <code>
/// var groups = ObservationTrends.Group(section.Items);
/// var lines = ObservationTrends.SeriesOf(groups[0].Records, TimeZoneInfo.Local);
/// </code>
/// </example>
public static class ObservationTrends
{
    /// <summary>How many records of one measure the history asks for.</summary>
    public const int HistorySize = 100;

    /// <summary>
    /// The measures of the records of a section, the one measured last first.
    /// </summary>
    /// <param name="items">The records (Observations), newest first; other types are left out.</param>
    /// <returns>The measures.</returns>
    /// <example>
    /// <code>
    /// var groups = ObservationTrends.Group(items);
    /// </code>
    /// </example>
    public static IReadOnlyList<TrendGroup> Group(IReadOnlyList<ResourceSummary> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var groups = new List<TrendGroup>();
        var byKey = new Dictionary<string, List<ResourceSummary>>(StringComparer.Ordinal);
        var searches = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var item in items.Where(item => item.ResourceType == "Observation" && item.Json is not null))
        {
            if (CodeOf(item.Json!) is not { } code)
            {
                continue;
            }

            if (!byKey.TryGetValue(code.Key, out var list))
            {
                list = [];
                byKey[code.Key] = list;
                searches[code.Key] = code.Search;
            }

            list.Add(item);
        }

        foreach (var (key, records) in byKey)
        {
            groups.Add(new TrendGroup(key, searches[key], records));
        }

        return groups;
    }

    /// <summary>
    /// The lines of a chart of the records of one measure.
    /// </summary>
    /// <param name="records">The records, in any order.</param>
    /// <param name="zone">The zone of the display.</param>
    /// <returns>The lines with points (the value of the observation first, then its components); none when no record has a number.</returns>
    /// <example>
    /// <code>
    /// var lines = ObservationTrends.SeriesOf(records, TimeZoneInfo.Local);
    /// </code>
    /// </example>
    public static IReadOnlyList<TrendSeries> SeriesOf(IReadOnlyList<ResourceSummary> records, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(zone);
        var lines = new Dictionary<string, List<(DateTime Moment, double Value, string? Unit)>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var record in records.Where(record => record.Json is not null).DistinctBy(record => record.Id))
        {
            if (ResourceMoment.Of(record.ResourceType, record.Json) is not { } moment)
            {
                continue;
            }

            var local = TimeZoneInfo.ConvertTime(moment, zone).DateTime;
            using var document = JsonDocument.Parse(record.Json!);
            var root = document.RootElement;
            if (QuantityOf(root, "valueQuantity") is { } own)
            {
                Add(NameOf(root) ?? "Value", local, own);
            }

            if (root.TryGetProperty("component", out var components) && components.ValueKind == JsonValueKind.Array)
            {
                foreach (var component in components.EnumerateArray())
                {
                    if (QuantityOf(component, "valueQuantity") is { } value)
                    {
                        Add(NameOf(component) ?? "Component", local, value);
                    }
                }
            }
        }

        return [.. order.Select(name =>
        {
            var points = lines[name];
            var unit = points.GroupBy(point => point.Unit).OrderByDescending(group => group.Count()).First().Key;
            return new TrendSeries(name, unit, [.. points.Where(point => point.Unit == unit).OrderBy(point => point.Moment).Select(point => new TrendPoint(point.Moment, point.Value))]);
        })];

        void Add(string name, DateTime moment, Quantity quantity)
        {
            if (!lines.TryGetValue(name, out var points))
            {
                points = [];
                lines[name] = points;
                order.Add(name);
            }

            points.Add((moment, quantity.Value, quantity.Unit));
        }
    }

    private static Quantity? QuantityOf(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var quantity) || !quantity.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var unit = quantity.TryGetProperty("unit", out var u) ? u.GetString() : quantity.TryGetProperty("code", out var c) ? c.GetString() : null;
        return new Quantity(value.GetDouble(), unit);
    }

    // The name of a code: the display of its first coding, else its text, else the code
    private static string? NameOf(JsonElement element)
    {
        if (!element.TryGetProperty("code", out var code))
        {
            return null;
        }

        if (code.TryGetProperty("coding", out var codings) && codings.ValueKind == JsonValueKind.Array && codings.GetArrayLength() > 0)
        {
            var first = codings[0];
            if (first.TryGetProperty("display", out var display) && display.GetString() is { Length: > 0 } shown)
            {
                return shown;
            }
        }

        if (code.TryGetProperty("text", out var text) && text.GetString() is { Length: > 0 } written)
        {
            return written;
        }

        return codings.ValueKind == JsonValueKind.Array && codings.GetArrayLength() > 0 && codings[0].TryGetProperty("code", out var bare) ? bare.GetString() : null;
    }

    private static CodeKey? CodeOf(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("code", out var code))
        {
            return null;
        }

        if (code.TryGetProperty("coding", out var codings) && codings.ValueKind == JsonValueKind.Array)
        {
            foreach (var coding in codings.EnumerateArray())
            {
                var system = coding.TryGetProperty("system", out var s) ? s.GetString() : null;
                if (coding.TryGetProperty("code", out var c) && c.GetString() is { Length: > 0 } value)
                {
                    var token = string.IsNullOrEmpty(system) ? value : $"{system}|{value}";
                    return new CodeKey(token, token);
                }
            }
        }

        return code.TryGetProperty("text", out var text) && text.GetString() is { Length: > 0 } written ? new CodeKey("text:" + written.ToLower(CultureInfo.InvariantCulture), null) : null;
    }

    private sealed record CodeKey(string Key, string? Search);

    private sealed record Quantity(double Value, string? Unit);
}
