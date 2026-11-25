using System.Globalization;
using System.Text.RegularExpressions;
using Hl7.Fhir.Model;

namespace FireChart.Viewer.Core.Reports;

// How an element reads as text, by its FHIR type: a name, a code, a quantity, a period, a date. Release-neutral (the children by name), so one
// formatter serves every release. Takes the place of the text mode of the templates
internal static partial class ReportText
{
    public static string Of(IEnumerable<PocoNode> nodes, ReportFormat culture)
    {
        return string.Join(", ", nodes.Select(node => Of(node, culture)).Where(text => text.Length > 0));
    }

    public static string Of(PocoNode node, ReportFormat culture)
    {
        if (node.Poco is PrimitiveType primitive)
        {
            return Primitive(primitive, culture);
        }

        return node.Poco.TypeName switch
        {
            "HumanName" => Text(node, "text", culture) is { Length: > 0 } text
                ? text
                : Join(" ", [.. Texts(node, "prefix", culture), .. Texts(node, "given", culture), Text(node, "family", culture), .. Texts(node, "suffix", culture)]),
            // The words of a code: its own display, else the terminology server's, else the text of the concept, else the bare code
            "CodeableConcept" => First([.. Children(node, "coding").Select(coding => Text(coding, "display", culture)),
                                        .. Children(node, "coding").Select(coding => Looked(coding, culture)),
                                        Text(node, "text", culture),
                                        .. Children(node, "coding").Select(coding => Text(coding, "code", culture))]),
            "Coding" => First(Text(node, "display", culture), Looked(node, culture), Text(node, "code", culture)),
            "Quantity" or "SimpleQuantity" or "Age" or "Duration" or "Distance" or "Count" or "MoneyQuantity" =>
                Join(" ", Text(node, "comparator", culture) + Text(node, "value", culture), First(Text(node, "unit", culture), Text(node, "code", culture))),
            "Range" => Join(" – ", Text(node, "low", culture), Text(node, "high", culture)),
            "Ratio" => Join(" / ", Text(node, "numerator", culture), Text(node, "denominator", culture)),
            "Period" => Period(node, culture),
            "Reference" => First(Text(node, "display", culture), Text(node, "reference", culture), Text(node, "identifier", culture)),
            "CodeableReference" => First(Text(node, "concept", culture), Text(node, "reference", culture)),
            "Timing" => First(Text(node, "code", culture), Text(node, "event", culture)),
            "Dosage" => First(Text(node, "text", culture), Text(node, "patientInstruction", culture)),
            "SampledData" => string.Empty,
            "Identifier" => Text(node, "value", culture),
            "ContactPoint" => Text(node, "value", culture),
            "Address" => Text(node, "text", culture) is { Length: > 0 } text
                ? text
                : Join(", ", [.. Texts(node, "line", culture), Join(" ", Text(node, "postalCode", culture), Text(node, "city", culture)), Text(node, "country", culture)]),
            "Annotation" => Text(node, "text", culture),
            "Attachment" => First(Text(node, "title", culture), Text(node, "url", culture)),
            "Money" => Join(" ", Text(node, "value", culture), Text(node, "currency", culture)),
            "Narrative" => string.Empty,
            _ when node.Poco is Resource => Join("/", node.Poco.TypeName, Text(node, "id", culture)),
            _ => Join(" ", [.. All(node).Where(child => child.Poco is PrimitiveType).Select(child => Of(child, culture))])
        };
    }

    // An age from a birth date as FHIR writes it (a year, a month, a day) to a day: whole years, else months, else days; null for a birth after the day
    // or a date that cannot be read
    public static ReportAge? AgeOf(string birth, DateOnly day)
    {
        var match = BirthRegex().Match(birth);
        if (!match.Success)
        {
            return null;
        }

        var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
        var date = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 1;
        if (month is < 1 or > 12 || date < 1 || date > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        var born = new DateOnly(year, month, date);
        if (born > day)
        {
            return null;
        }

        var years = day.Year - born.Year - (day < born.AddYears(day.Year - born.Year) ? 1 : 0);
        if (years > 0)
        {
            return new ReportAge(years, ReportAgeUnit.Years);
        }

        var months = (day.Year - born.Year) * 12 + day.Month - born.Month - (day.Day < born.Day ? 1 : 0);
        return months > 0 ? new ReportAge(months, ReportAgeUnit.Months) : new ReportAge(day.DayNumber - born.DayNumber, ReportAgeUnit.Days);
    }

    // The day of a moment of an event: a date, a dateTime (in the zone of the display when it has an offset) or the start of a Period; null for anything
    // else (a deceasedBoolean)
    public static DateOnly? DayOfEvent(PocoNode node, ReportFormat format)
    {
        if (node.Poco.TypeName == "Period")
        {
            return Children(node, "start").FirstOrDefault() is { } start ? DayOfEvent(start, format) : null;
        }

        var text = node.Poco is PrimitiveType { JsonValue: { } value } ? value.ToString() ?? string.Empty : string.Empty;
        return DayOf(text, format) ?? (DayRegex().IsMatch(text) ? DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null);
    }

    // The title of a record with no component of its own: what it is about (a code, a name, a title), else its type and id
    public static string TitleOf(PocoNode record, ReportFormat culture)
    {
        // The first of a repeated element (a patient with two names is called by the first)
        foreach (var name in s_titles)
        {
            if (Texts(record, name, culture).FirstOrDefault() is { Length: > 0 } text)
            {
                return text;
            }
        }

        return Join("/", record.Poco.TypeName, Text(record, "id", culture));
    }

    // The element that says when a record happened (effective, onset, ...); null when it has none
    public static PocoNode? MomentOf(PocoNode record)
    {
        return s_moments.Select(name => Children(record, name).FirstOrDefault()).FirstOrDefault(node => node is not null);
    }

    // When a record happened, for the second line of its tile
    public static string WhenOf(PocoNode record, ReportFormat culture)
    {
        return First(s_moments.Select(name => Text(record, name, culture)));
    }

    // The elements of a record as label and text, for the view of a type with no component of its own; what the page shows apart (the id, the
    // narrative, the metadata, the contained resources) is left out
    public static IEnumerable<ReportField> FieldsOf(PocoNode record, ReportFormat culture)
    {
        return All(record).Where(child => !s_hidden.Contains(child.Name))
                          .GroupBy(child => child.Name)
                          .Select(group => new ReportField(Label(group.Key), string.Join(", ", group.Select(child => Of(child, culture)).Where(text => text.Length > 0))))
                          .Where(field => field.Text.Length > 0);
    }

    // birthDate: "Birth date"
    private static string Label(string name)
    {
        var words = System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static readonly string[] s_titles = ["code", "name", "title", "type", "description", "vaccineCode", "medication", "category"];

    private static readonly string[] s_moments = ["effective", "period", "onset", "occurrence", "performed", "authoredOn", "recordedDate", "issued", "date",
                                                  "created", "start"];

    private static readonly HashSet<string> s_hidden = new(StringComparer.Ordinal) { "id", "meta", "text", "contained", "implicitRules", "language" };

    private static string Primitive(PrimitiveType primitive, ReportFormat culture)
    {
        var value = primitive.JsonValue;
        return primitive.TypeName switch
        {
            "date" or "dateTime" or "instant" => Date(value?.ToString() ?? string.Empty, culture),
            "boolean" => value is true ? "yes" : value is false ? "no" : value?.ToString() ?? string.Empty,
            "decimal" when value is decimal number => DecimalText.Format(number, culture.Decimals, culture.Culture),
            _ => Convert.ToString(value, culture.Culture) ?? string.Empty
        };
    }

    // A year stays. A date and a month are written in the culture and never moved by a zone (a birth date must not move by a day). A moment with an
    // offset is shown in the zone of the display; a moment without one is not converted (nobody knows its zone) and says so
    internal static string Date(string text, ReportFormat format)
    {
        var culture = format.Culture;
        if (MomentRegex().Match(text) is { Success: true } match)
        {
            var recorded = DateTime.ParseExact($"{match.Groups[1].Value} {match.Groups[2].Value}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            if (match.Groups[3].Value.Length == 0)
            {
                return $"{recorded.ToString("g", culture)} (no time zone)";
            }

            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
                ? TimeZoneInfo.ConvertTime(moment, format.Zone).DateTime.ToString("g", culture)
                : recorded.ToString("g", culture);
        }

        if (DayRegex().IsMatch(text) && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day.ToString("d", culture);
        }

        if (MonthRegex().IsMatch(text) && DateOnly.TryParseExact(text + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            return month.ToString(culture.DateTimeFormat.YearMonthPattern, culture);
        }

        return text;
    }

    // A period: its end as a time alone when it ends on the day it starts (in the zone of the display): "3/1/2020 9:00 AM – 10:30 AM"
    private static string Period(PocoNode node, ReportFormat format)
    {
        var start = Children(node, "start").FirstOrDefault()?.Poco is PrimitiveType { JsonValue: { } first } ? first.ToString() ?? string.Empty : string.Empty;
        var end = Children(node, "end").FirstOrDefault()?.Poco is PrimitiveType { JsonValue: { } last } ? last.ToString() ?? string.Empty : string.Empty;
        if (DayOf(start, format) is { } startDay && startDay == DayOf(end, format) && MomentRegex().Match(end) is { Success: true } moment)
        {
            var time = moment.Groups[3].Value.Length > 0 && DateTimeOffset.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
                ? TimeZoneInfo.ConvertTime(at, format.Zone).DateTime.ToString("t", format.Culture)
                : $"{DateTime.ParseExact(moment.Groups[2].Value, "HH:mm", CultureInfo.InvariantCulture).ToString("t", format.Culture)} (no time zone)";
            return Join(" – ", Date(start, format), time);
        }

        return Join(" – ", Date(start, format), Date(end, format));
    }

    // The day of a moment in the zone of the display (of a moment without an offset, its recorded day); null for anything that is not a moment
    private static DateOnly? DayOf(string text, ReportFormat format)
    {
        if (MomentRegex().Match(text) is not { Success: true } match)
        {
            return null;
        }

        return match.Groups[3].Value.Length > 0 && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
            ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, format.Zone).DateTime)
            : DateOnly.ParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    // The word the terminology server gives for a coding without a display
    private static string Looked(PocoNode coding, ReportFormat format)
    {
        var system = Text(coding, "system", format);
        var code = Text(coding, "code", format);
        return system.Length > 0 && code.Length > 0 && Text(coding, "display", format).Length == 0 ? format.DisplayOf(system, code) ?? string.Empty : string.Empty;
    }

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})(?::\d{2}(?:\.\d+)?)?(Z|[+-]\d{2}:\d{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex MomentRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex DayRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex MonthRegex();

    [GeneratedRegex(@"^(\d{4})(?:-(\d{2}))?(?:-(\d{2}))?", RegexOptions.CultureInvariant)]
    private static partial Regex BirthRegex();

    // The children of a name, single or repeated, as one sequence
    internal static IEnumerable<PocoNode> Children(PocoNode node, string name)
    {
        return All(node).Where(child => child.Name == name);
    }

    internal static IEnumerable<PocoNode> All(PocoNode node)
    {
        return node.Children().SelectMany(child => child switch
        {
            PocoNode single => [single],
            IEnumerable<PocoNode> list => list,
            _ => []
        });
    }

    // The text of the children of a name, joined
    internal static string Text(PocoNode node, string name, ReportFormat culture)
    {
        return string.Join(" ", Texts(node, name, culture));
    }

    private static IEnumerable<string> Texts(PocoNode node, string name, ReportFormat culture)
    {
        return Children(node, name).Select(child => Of(child, culture)).Where(text => text.Length > 0);
    }

    private static string First(params IEnumerable<string> texts)
    {
        return texts.FirstOrDefault(text => text.Length > 0) ?? string.Empty;
    }

    private static string Join(string separator, params IEnumerable<string> texts)
    {
        return string.Join(separator, texts.Where(text => text.Length > 0));
    }
}

// An age: whole years, else months, else days
internal sealed record ReportAge(int Count, ReportAgeUnit Unit);

internal enum ReportAgeUnit
{
    Years,
    Months,
    Days
}

// An element of a record as the view of a type with no component of its own shows it
internal sealed record ReportField(string Label, string Text);
