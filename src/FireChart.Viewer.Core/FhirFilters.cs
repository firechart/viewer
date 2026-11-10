using System.Globalization;
using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;

namespace FireChart.Viewer.Core;

// The filters of the view templates: what plain Liquid cannot do with the values of FHIR
internal static partial class FhirFilters
{
    private static readonly Dictionary<string, FluidValue> s_noArguments = [];

    internal static void Register(FilterCollection filters, TimeProvider clock, IReadOnlyList<ITemplateTexts> texts)
    {
        // One value, a list or nothing, as a list: a loop does not care how the element is declared in the release
        filters.AddFilter("as_list", (input, _, _) => ValueTask.FromResult<FluidValue>(input is ArrayValue ? input : input.IsNil() ? new ArrayValue([]) : new ArrayValue([input])));
        filters.AddFilter("age", (input, _, _) => ValueTask.FromResult(Age(input, DateOnly.FromDateTime(clock.GetLocalNow().DateTime))));
        filters.AddFilter("age_at", (input, arguments, _) => ValueTask.FromResult(AgeAt(input, arguments.At(0))));
        filters.AddFilter("fhir_date", (input, _, _) => ValueTask.FromResult<FluidValue>(input.IsNil() ? input : new StringValue(Date(input.ToStringValue(), clock.LocalTimeZone))));
        // fhir_number: decimals rounds to that many places when a field asks for it (nil: the digits of the record)
        filters.AddFilter("fhir_number", (input, arguments, _) => ValueTask.FromResult<FluidValue>(input.IsNil() ? input : new StringValue(Number(input.ToStringValue(), null, Places(arguments.At(0))))));
        // The calendar day of a moment in the zone of the display (a date stays as it is), yyyy-MM-dd: two records compared by day
        filters.AddFilter("fhir_day", (input, _, _) => ValueTask.FromResult(Day(input, clock.LocalTimeZone)));
        // The time of a moment alone, as fhir_date writes it (the zone of the display, "(no time zone)" without an offset): the end of a period of one day
        filters.AddFilter("fhir_time", (input, _, _) => ValueTask.FromResult(TimeOf(input, clock.LocalTimeZone)));
        filters.AddFilter("fhir_bool", (input, _, _) => ValueTask.FromResult<FluidValue>(input.IsNil() ? input : new StringValue(Bool(input.ToStringValue(), texts))));
        filters.AddFilter("fhir_href", (input, _, _) => ValueTask.FromResult(Href(input)));
    }

    // "49 y", "7 mo", "12 d" from a date as FHIR writes it (a year, a month, a day; a time is ignored)
    public static FluidValue Age(FluidValue input, DateOnly today)
    {
        if (input.IsNil() || BirthDate().Match(input.ToStringValue()) is not { Success: true } match)
        {
            return NilValue.Instance;
        }

        var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
        var day = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 1;
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return NilValue.Instance;
        }

        var born = new DateOnly(year, month, day);
        if (born > today)
        {
            return NilValue.Instance;
        }

        var years = today.Year - born.Year - (today < born.AddYears(today.Year - born.Year) ? 1 : 0);
        if (years > 0)
        {
            return new StringValue($"{years} y");
        }

        var months = (today.Year - born.Year) * 12 + today.Month - born.Month - (today.Day < born.Day ? 1 : 0);
        return new StringValue(months > 0 ? $"{months} mo" : $"{today.DayNumber - born.DayNumber} d");
    }

    // The age at the moment of an event ("34 y"): a date, a dateTime or a Period (its start); the zone of the event does not matter, only its date
    private static FluidValue AgeAt(FluidValue birth, FluidValue when)
    {
        var text = when is FhirNodeValue { Node: { } node } && node.Children("start") is [{ } start, ..] ? start.ValueText() : when.IsNil() ? null : when.ToStringValue();
        return text is not null && EventDate().Match(text) is { Success: true } match && DateOnly.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? Age(birth, day)
            : NilValue.Instance;
    }

    // The culture of the application shapes what the user reads (CultureInfo.CurrentCulture, set from the language setting); FHIR itself is parsed invariantly.
    // A year stays. A date and a month are written in the culture and never moved by a zone. A time with an offset is shown in the zone of the display,
    // a time without an offset is not converted (nobody knows its zone) and says so
    public static string Date(string value, TimeZoneInfo? zone = null, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (Moment().Match(value) is { Success: true } match)
        {
            return Time(value, match, zone, culture);
        }

        if (DayOnly().IsMatch(value) && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day.ToString("d", culture);
        }

        if (MonthOnly().IsMatch(value) && DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            return month.ToString(culture.DateTimeFormat.YearMonthPattern, culture);
        }

        return value;
    }

    private static string Time(string value, Match match, TimeZoneInfo? zone, CultureInfo culture)
    {
        var recorded = DateTime.ParseExact($"{match.Groups[1].Value} {match.Groups[2].Value}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (match.Groups[3].Value.Length == 0)
        {
            return $"{recorded.ToString("g", culture)} (no time zone)";
        }

        if (zone is null || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment))
        {
            return recorded.ToString("g", culture);
        }

        return TimeZoneInfo.ConvertTime(moment, zone).DateTime.ToString("g", culture);
    }

    // A decimal as the culture writes it, with the digits the record has ("1.30" is "1,30" in German: the trailing zeros are significant figures). decimals (a field
    // that asks for it) is the most places shown: more are rounded half away from zero, fewer are never padded (1.3 stays 1.3: zeros would claim a precision
    // the record does not have)
    public static string Number(string value, CultureInfo? culture = null, int? decimals = null)
    {
        if (!Decimal().IsMatch(value) || !decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
        {
            return value;
        }

        var point = value.IndexOf('.', StringComparison.Ordinal);
        var digits = point < 0 ? 0 : value.Length - point - 1;
        if (decimals is { } most && most < digits)
        {
            number = Math.Round(number, most, MidpointRounding.AwayFromZero);
            digits = most;
        }

        return number.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), culture ?? CultureInfo.CurrentCulture);
    }

    // nil for a date, a month, a year or anything that is not a moment
    private static FluidValue TimeOf(FluidValue input, TimeZoneInfo zone, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var text = input.IsNil() ? null : input.ToStringValue();
        if (text is null || Moment().Match(text) is not { Success: true } match)
        {
            return NilValue.Instance;
        }

        var recorded = DateTime.ParseExact($"{match.Groups[1].Value} {match.Groups[2].Value}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (match.Groups[3].Value.Length == 0)
        {
            return new StringValue($"{recorded.ToString("t", culture)} (no time zone)");
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
            ? new StringValue(TimeZoneInfo.ConvertTime(moment, zone).DateTime.ToString("t", culture))
            : new StringValue(recorded.ToString("t", culture));
    }

    // The places a template asks for: a whole number from 0 to 10, else none
    private static int? Places(FluidValue value)
    {
        if (value.IsNil())
        {
            return null;
        }

        var number = value.ToNumberValue();
        return number is >= 0 and <= 10 && number == decimal.Truncate(number) ? (int)number : null;
    }

    // yyyy-MM-dd of a date, a dateTime (in the zone of the display when it has an offset) or a Period (its start); nil for anything else
    private static FluidValue Day(FluidValue input, TimeZoneInfo zone)
    {
        var text = input is FhirNodeValue { Node: { } node } && node.Children("start") is [{ } start, ..] ? start.ValueText() : input.IsNil() ? null : input.ToStringValue();
        if (text is null)
        {
            return NilValue.Instance;
        }

        if (Moment().Match(text) is { Success: true } match)
        {
            return match.Groups[3].Value.Length > 0 && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
                ? new StringValue(TimeZoneInfo.ConvertTime(moment, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                : new StringValue(match.Groups[1].Value);
        }

        return DayOnly().IsMatch(text) ? new StringValue(text) : NilValue.Instance;
    }

    // true and false as words of the language of the application (the engine texts Yes and No of the locales)
    public static string Bool(string value, IReadOnlyList<ITemplateTexts> texts, CultureInfo? culture = null)
    {
        return value switch
        {
            "true" => TemplateText.Translate(texts, EngineTexts.Yes, s_noArguments, culture),
            "false" => TemplateText.Translate(texts, EngineTexts.No, s_noArguments, culture),
            _ => value
        };
    }

    // "fhir:Patient/1" for a Reference (or the text of one) that points to a resource of a server; nothing for a contained or a urn reference
    private static FluidValue Href(FluidValue input)
    {
        var reference = input is FhirNodeValue node ? node.Node.Children("reference") is [{ } first, ..] ? first.ValueText() : null : input.IsNil() ? null : input.ToStringValue();
        return ResourcePool.KeyOf(reference) is { } key && Key().IsMatch(key) ? new StringValue(FxHtml.FhirScheme + key) : NilValue.Instance;
    }

    [GeneratedRegex(@"^(\d{4})(?:-(\d{2}))?(?:-(\d{2}))?", RegexOptions.CultureInvariant)]
    private static partial Regex BirthDate();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex DayOnly();

    [GeneratedRegex(@"^\d{4}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex MonthOnly();

    [GeneratedRegex(@"^-?\d+(\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Decimal();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})(?::\d{2}(?:\.\d+)?)?(Z|[+-]\d{2}:\d{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex Moment();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex EventDate();

    [GeneratedRegex("^[A-Za-z]{1,64}/[A-Za-z0-9.-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Key();
}
