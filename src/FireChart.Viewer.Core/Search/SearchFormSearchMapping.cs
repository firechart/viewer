using System.Globalization;

using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Core.Search;

/// <summary>
/// What a form read from a search.
/// </summary>
/// <param name="Values">The values of its fields, by <see cref="SearchFormField.QueryName"/>; a field the search does not have is empty (its default
/// comparator, modifier and unit, not its default value: the defaults of a form are the first search of a list, <see cref="SearchFormSearchMapping.Defaults"/>).</param>
/// <param name="Consumed">The parameters it took, and so owns: the rest of the search is not the form's and stays when the form is applied again.</param>
/// <example>
/// <code>
/// var read = SearchFormSearchMapping.Read(definition, search);
/// var foreign = search.Parameters.Count - read.Consumed.Count;
/// </code>
/// </example>
public sealed record SearchFormRead
(
    IReadOnlyDictionary<string, SearchFieldValue> Values,
    IReadOnlyList<FhirSearchParameter> Consumed
);

/// <summary>
/// A form and the search of an address are two writings of the same thing: the conditions of the form are parameters of a FHIR search, and a search
/// written by hand (or kept in a link) fills the form.
/// </summary>
/// <remarks>
/// What a field cannot show (two alternatives in a text field, a date with a time) is not taken by the form and stays in the search as it is.
/// </remarks>
/// <example>
/// <code>
/// var read = SearchFormSearchMapping.Read(definition, search);
/// // the user changes read.Values in the form
/// var next = SearchFormSearchMapping.Apply(search, read, SearchFormEvaluator.Evaluate(definition, read.Values));
/// </code>
/// </example>
public static class SearchFormSearchMapping
{
    private static readonly string[] s_prefixes = ["eq", "ne", "gt", "lt", "ge", "le", "sa", "eb", "ap"];

    /// <summary>
    /// The conditions of a form as parameters of a search, one for a condition (a range is two parameters of the same name).
    /// </summary>
    /// <param name="conditions">The conditions.</param>
    /// <returns>The parameters.</returns>
    public static IReadOnlyList<FhirSearchParameter> ToParameters(IEnumerable<SearchCondition> conditions)
    {
        return [.. conditions.Select(row => new FhirSearchParameter(row.Name, row.Modifier, [.. row.Values.Select(value => row.Comparator + value)]))];
    }

    /// <summary>
    /// Fills a form from a search.
    /// </summary>
    /// <param name="definition">The form.</param>
    /// <param name="search">The search of the address.</param>
    /// <returns>The values of the fields and the parameters the form took.</returns>
    public static SearchFormRead Read(SearchFormDefinition definition, FhirSearchState search)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(search);
        var values = new Dictionary<string, SearchFieldValue>(StringComparer.Ordinal);
        var consumed = new List<FhirSearchParameter>();
        foreach (var field in definition.Fields)
        {
            var own = search.Parameters.Where(row => !consumed.Contains(row) && Modifier(field, row) is not null)
                            .ToList();
            var value = own.Count == 0 ? null : ReadField(field, own, consumed);
            values[field.QueryName] = value ?? Blank(field);
        }

        return new SearchFormRead(values, consumed);
    }

    /// <summary>
    /// The search the default values of a form make: what a list starts with when it was never searched.
    /// </summary>
    /// <param name="definition">The form.</param>
    /// <returns>The search; empty when no field has a default value.</returns>
    /// <example>
    /// <code>
    /// var first = SearchFormSearchMapping.Defaults(definition).ToQueryString();   // kind=resource
    /// </code>
    /// </example>
    public static FhirSearchState Defaults(SearchFormDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var values = new Dictionary<string, SearchFieldValue>(StringComparer.Ordinal);
        foreach (var field in definition.Fields)
        {
            values[field.QueryName] = SearchFormEvaluator.NewValue(field);
        }

        return FhirSearchState.Empty.Replace([], ToParameters(SearchFormEvaluator.Evaluate(definition, values)));
    }

    // A field without a value: how it is entered (the default comparator, modifier, unit), not what
    private static SearchFieldValue Blank(SearchFormField field)
    {
        var value = SearchFormEvaluator.NewValue(field);
        value.Text = null;
        value.Selected.Clear();
        return value;
    }

    /// <summary>
    /// The search after a form is applied: the parameters the form read are replaced by the conditions it makes now; the others stay.
    /// </summary>
    /// <param name="search">The search the form was read from.</param>
    /// <param name="before">What the form read from it (<see cref="Read"/>).</param>
    /// <param name="conditions">The conditions of the form now.</param>
    /// <returns>The new search.</returns>
    public static FhirSearchState Apply(FhirSearchState search, SearchFormRead before, IEnumerable<SearchCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(before);
        return search.Replace(before.Consumed, ToParameters(conditions));
    }

    // The modifier of a parameter of the field ("" when it has none), or null when the parameter is not the field's. A query name may hold a colon
    // itself ("subject:Patient.name"), so the name of the field is compared with the whole name of the parameter
    private static string? Modifier(SearchFormField field, FhirSearchParameter parameter)
    {
        var full = parameter.Modifier is null ? parameter.Name : $"{parameter.Name}:{parameter.Modifier}";
        if (full == field.QueryName)
        {
            return string.Empty;
        }

        return full.StartsWith(field.QueryName + ":", StringComparison.Ordinal) ? full[(field.QueryName.Length + 1)..] : null;
    }

    private static SearchFieldValue? ReadField(SearchFormField field, List<FhirSearchParameter> own, List<FhirSearchParameter> consumed)
    {
        var value = new SearchFieldValue();
        bool Take(FhirSearchParameter parameter)
        {
            consumed.Add(parameter);
            value.Modifier ??= Modifier(field, parameter) is { Length: > 0 } modifier ? modifier : null;
            return true;
        }

        var any = false;
        switch (field.Control)
        {
            case SearchFieldControl.Text:
            case SearchFieldControl.Autocomplete:
            case SearchFieldControl.Reference:
            case SearchFieldControl.Boolean:
                if (own[0].Values is [var text])
                {
                    value.Text = FhirSearchQuery.Unescape(text);
                    any = Take(own[0]);
                }

                break;

            case SearchFieldControl.Number:
                if (Single(own[0]) is { } number && SplitComparator(number) is var split && decimal.TryParse(split.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    value.Number = parsed;
                    value.Comparator = split.Comparator ?? "eq";
                    any = Take(own[0]);
                }

                break;

            case SearchFieldControl.NumberRange:
                foreach (var parameter in own)
                {
                    if (Single(parameter) is { } bound && SplitComparator(bound) is var bsplit && decimal.TryParse(bsplit.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
                    {
                        if (bsplit.Comparator == "ge" && value.Number is null)
                        {
                            value.Number = n;
                            any = Take(parameter);
                        }
                        else if (bsplit.Comparator == "le" && value.NumberTo is null)
                        {
                            value.NumberTo = n;
                            any = Take(parameter);
                        }
                    }
                }

                break;

            case SearchFieldControl.Date:
                if (Single(own[0]) is { } date && SplitComparator(date) is var dsplit && ParseDate(dsplit.Text) is { } day)
                {
                    value.Date = day;
                    value.Comparator = dsplit.Comparator ?? "eq";
                    any = Take(own[0]);
                }

                break;

            case SearchFieldControl.DateRange:
                foreach (var parameter in own)
                {
                    if (Single(parameter) is { } bound && SplitComparator(bound) is var dbound && ParseDate(dbound.Text) is { } d)
                    {
                        if (dbound.Comparator == "ge" && value.Date is null)
                        {
                            value.Date = d;
                            any = Take(parameter);
                        }
                        else if (dbound.Comparator == "le" && value.DateTo is null)
                        {
                            value.DateTo = d;
                            any = Take(parameter);
                        }
                    }
                }

                break;

            case SearchFieldControl.Select:
            case SearchFieldControl.MultiSelect:
                // The tokens as they are (system|code): they are the keys of the options
                value.Selected.AddRange(own[0].Values);
                any = Take(own[0]);
                break;

            case SearchFieldControl.Quantity:
                if (Single(own[0]) is { } quantity && SplitComparator(quantity) is var qsplit)
                {
                    var parts = FhirSearchQuery.SplitUnescaped(qsplit.Text, '|');
                    if (decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && (parts.Count == 1 || (parts.Count == 3 && parts[1] == SearchFhirFormat.UcumSystem)))
                    {
                        value.Number = amount;
                        value.Comparator = qsplit.Comparator ?? "eq";
                        value.Unit = parts.Count == 3 ? FhirSearchQuery.Unescape(parts[2]) : null;
                        any = Take(own[0]);
                    }
                }

                break;

            case SearchFieldControl.Identifier:
                if (Single(own[0]) is { } identifier)
                {
                    var parts = FhirSearchQuery.SplitUnescaped(identifier, '|');
                    if (parts.Count <= 2)
                    {
                        value.System = parts.Count == 2 && parts[0].Length > 0 ? FhirSearchQuery.Unescape(parts[0]) : null;
                        value.Text = FhirSearchQuery.Unescape(parts[^1]) is { Length: > 0 } id ? id : null;
                        any = Take(own[0]);
                    }
                }

                break;
        }

        return any ? value : null;
    }

    // The only alternative of a parameter; null when it has several (a field of these controls holds one)
    private static string? Single(FhirSearchParameter parameter)
    {
        return parameter.Values is [var only] ? only : null;
    }

    private readonly record struct Prefixed(string? Comparator, string Text);

    // "ge2000-01-01" -> prefix ge, text 2000-01-01; a value without a prefix has none
    private static Prefixed SplitComparator(string value)
    {
        if (value.Length > 2 && s_prefixes.FirstOrDefault(prefix => value.StartsWith(prefix, StringComparison.Ordinal)) is { } prefix && (char.IsDigit(value[2]) || value[2] is '-' or '+' or '.'))
        {
            return new Prefixed(prefix, value[2..]);
        }

        return new Prefixed(null, value);
    }

    private static DateTime? ParseDate(string text)
    {
        return DateTime.TryParseExact(text, SearchFhirFormat.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ? result : null;
    }
}
