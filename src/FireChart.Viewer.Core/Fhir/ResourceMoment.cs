using System.Globalization;
using System.Text.Json;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// The moment a record belongs to on a timeline, from the JSON of any release (the element differs by type and by release).
/// </summary>
/// <example>
/// <code>
/// var newestFirst = items.OrderByDescending(item =&gt; ResourceMoment.Of(item.ResourceType, item.Json));
/// </code>
/// </example>
public static class ResourceMoment
{
    // The first element that is there wins; an element is a date or a Period (its start)
    private static readonly Dictionary<string, string[]> s_elements = new(StringComparer.Ordinal)
    {
        ["Condition"] = ["recordedDate", "onsetDateTime", "onsetPeriod"],
        ["Encounter"] = ["period", "actualPeriod"],
        ["Procedure"] = ["performedDateTime", "performedPeriod", "occurrenceDateTime", "occurrencePeriod"],
        ["Observation"] = ["effectiveDateTime", "effectiveInstant", "effectivePeriod", "issued"],
        ["AllergyIntolerance"] = ["recordedDate", "onsetDateTime", "onsetPeriod"],
        ["MedicationStatement"] = ["effectiveDateTime", "effectivePeriod", "dateAsserted"],
        ["MedicationRequest"] = ["authoredOn"],
        ["Immunization"] = ["occurrenceDateTime", "recorded"],
        ["DiagnosticReport"] = ["effectiveDateTime", "effectivePeriod", "issued"],
        ["DeviceUseStatement"] = ["timingDateTime", "timingPeriod", "recordedOn"],
        ["CarePlan"] = ["period", "created"],
        ["Consent"] = ["dateTime"]
    };

    /// <summary>
    /// The moment of a record: the first of the elements of its type that is there (a date, or the start of a <c>Period</c>).
    /// </summary>
    /// <param name="resourceType">The type (<c>Observation</c>).</param>
    /// <param name="json">The resource as JSON.</param>
    /// <returns>The moment, or <see langword="null"/> when the type has no known element, the record has none, or the JSON cannot be read.</returns>
    /// <example>
    /// <code>
    /// var when = ResourceMoment.Of("Observation", json);
    /// </code>
    /// </example>
    public static DateTimeOffset? Of(string resourceType, string? json)
    {
        if (string.IsNullOrEmpty(json) || !s_elements.TryGetValue(resourceType, out var names))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var name in names)
            {
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(name, out var element) && Moment(element) is { } moment)
                {
                    return moment;
                }
            }
        }
        catch (JsonException)
        {
            // a record that cannot be read has no moment
        }

        return null;
    }

    /// <summary>
    /// Reads a FHIR date, dateTime or instant. A date without a time is the start of that day; a time without an offset is taken as UTC.
    /// </summary>
    /// <param name="text">The value (<c>2024</c>, <c>2024-05</c>, <c>2024-05-17</c>, <c>2024-05-17T10:30:00+02:00</c>).</param>
    /// <returns>The moment, or <see langword="null"/> when the text is not a date.</returns>
    /// <example>
    /// <code>
    /// var start = ResourceMoment.Parse("2024-05"); // 2024-05-01T00:00:00Z
    /// </code>
    /// </example>
    public static DateTimeOffset? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length == 4 && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year > 0)
        {
            return new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment) ? moment : null;
    }

    private static DateTimeOffset? Moment(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => Parse(element.GetString()),
            JsonValueKind.Object when element.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String => Parse(start.GetString()),
            _ => null
        };
    }
}
