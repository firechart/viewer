using System.Globalization;
using System.Text.RegularExpressions;

using FireChart.Viewer.Core.Forms;
using FloxStrings = Flox.Resources.Strings;

namespace FireChart.Viewer.Core.Search;

/// <summary>
/// The rules of a search form: what the value of a field means as a search, and whether it is acceptable.
/// </summary>
/// <example>
/// <code>
/// var values = definition.Fields.ToDictionary(field =&gt; field.QueryName, SearchFormEvaluator.NewValue);
/// values["family"].Text = "Smith";
/// var conditions = SearchFormEvaluator.Evaluate(definition, values);
/// </code>
/// </example>
public static class SearchFormEvaluator
{
    private const string EqualComparator = "eq";

    /// <summary>
    /// The value of a field when the form is opened empty: its defaults (comparator, modifier, unit, value).
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>A new value.</returns>
    public static SearchFieldValue NewValue(SearchFormField field)
    {
        var result = new SearchFieldValue
        {
            Comparator = field.DefaultComparator,
            Modifier = field.DefaultModifier,
            Unit = field.DefaultUnit,
            Text = field.DefaultValue is { Length: > 0 } text && field.Control is not (SearchFieldControl.Select or SearchFieldControl.MultiSelect) ? text : null
        };
        result.Selected.AddRange(field.DefaultValue is { Length: > 0 } code && field.Control is SearchFieldControl.Select or SearchFieldControl.MultiSelect ? [code] : []);
        return result;
    }

    /// <summary>
    /// The conditions of a form.
    /// </summary>
    /// <param name="definition">The form.</param>
    /// <param name="values">The values by <see cref="SearchFormField.QueryName"/>; a field without a value makes no condition.</param>
    /// <returns>The conditions in the order of the fields.</returns>
    public static List<SearchCondition> Evaluate(SearchFormDefinition definition, IReadOnlyDictionary<string, SearchFieldValue> values)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var result = new List<SearchCondition>();
        foreach (var field in definition.Fields)
        {
            if (values.TryGetValue(field.QueryName, out var value))
            {
                result.AddRange(Evaluate(field, value));
            }
        }

        return result;
    }

    /// <summary>
    /// The conditions of one field: none when it is empty, two for a range.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="value">Its value.</param>
    /// <returns>The conditions.</returns>
    public static IEnumerable<SearchCondition> Evaluate(SearchFormField field, SearchFieldValue value)
    {
        var modifier = Blank(value.Modifier);
        var comparator = Comparator(value.Comparator);
        var type = field.ParameterType;

        SearchCondition Make(string? cmp, params string[] values)
        {
            return new SearchCondition(field.QueryName, modifier, cmp, type, values);
        }

        switch (field.Control)
        {
            case SearchFieldControl.Text:
            case SearchFieldControl.Autocomplete:
            case SearchFieldControl.Reference:
                if (Blank(value.Text) is { } text)
                {
                    yield return Make(null, SearchFhirFormat.Escape(text));
                }

                break;

            case SearchFieldControl.Boolean:
                if (Blank(value.Text) is { } flag)
                {
                    yield return Make(null, flag);
                }

                break;

            case SearchFieldControl.Number:
                if (value.Number is { } number)
                {
                    yield return Make(comparator, SearchFhirFormat.Number(number));
                }

                break;

            case SearchFieldControl.NumberRange:
                if (value.Number is { } from)
                {
                    yield return Make("ge", SearchFhirFormat.Number(from));
                }

                if (value.NumberTo is { } to)
                {
                    yield return Make("le", SearchFhirFormat.Number(to));
                }

                break;

            case SearchFieldControl.Date:
                if (value.Date is { } date)
                {
                    yield return Make(comparator, SearchFhirFormat.Date(date));
                }

                break;

            case SearchFieldControl.DateRange:
                if (value.Date is { } dateFrom)
                {
                    yield return Make("ge", SearchFhirFormat.Date(dateFrom));
                }

                if (value.DateTo is { } dateTo)
                {
                    yield return Make("le", SearchFhirFormat.Date(dateTo));
                }

                break;

            case SearchFieldControl.Select:
            case SearchFieldControl.MultiSelect:
                var selected = value.Selected
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .ToArray();
                if (selected.Length > 0)
                {
                    yield return Make(null, selected);
                }
                else if (Blank(value.Text) is { } typed)
                {
                    yield return Make(null, SearchFhirFormat.Escape(typed));
                }

                break;

            case SearchFieldControl.Quantity:
                if (value.Number is { } quantity)
                {
                    var unit = Blank(value.Unit);
                    var quantityText = unit == null ? SearchFhirFormat.Number(quantity) : $"{SearchFhirFormat.Number(quantity)}|{SearchFhirFormat.UcumSystem}|{SearchFhirFormat.Escape(unit)}";
                    yield return Make(comparator, quantityText);
                }

                break;

            case SearchFieldControl.Identifier:
                var system = Blank(value.System);
                var identifier = Blank(value.Text);
                if (system != null || identifier != null)
                {
                    var systemPart = system == null ? string.Empty : SearchFhirFormat.Escape(system) + "|";
                    yield return Make(null, systemPart + (identifier == null ? string.Empty : SearchFhirFormat.Escape(identifier)));
                }

                break;
        }
    }

    /// <summary>
    /// The value makes a condition.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="value">Its value.</param>
    /// <returns><see langword="true"/> when <see cref="Evaluate(SearchFormField, SearchFieldValue)"/> has a condition.</returns>
    public static bool HasValue(SearchFormField field, SearchFieldValue value)
    {
        return Evaluate(field, value)
           .Any();
    }

    /// <summary>
    /// The first problem of a value: required first, then the length, the pattern and the bounds of the field.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="value">Its value.</param>
    /// <returns>The text of the problem in the current UI culture (the common texts of Flox), or <see langword="null"/> when the value is fine.</returns>
    /// <example>
    /// <code>
    /// var problem = SearchFormEvaluator.Validate(field, value);
    /// if (problem is not null)
    /// {
    ///     errors[field.QueryName] = problem;
    /// }
    /// </code>
    /// </example>
    public static string? Validate(SearchFormField field, SearchFieldValue value)
    {
        if (!HasValue(field, value))
        {
            return field.Required ? FloxStrings.Message_FieldRequired : null;
        }

        if (field.Control is SearchFieldControl.Text or SearchFieldControl.Autocomplete or SearchFieldControl.Reference && Blank(value.Text) is { } text)
        {
            var invalid = (field.MinLength is { } min && text.Length < min) || (field.MaxLength is { } max && text.Length > max) || (!string.IsNullOrEmpty(field.Pattern) && !Matches(field.Pattern, text));
            return invalid ? FloxStrings.Message_InvalidValue : null;
        }

        if (field.Control is SearchFieldControl.Number or SearchFieldControl.NumberRange or SearchFieldControl.Quantity)
        {
            var invalid = Outside(field, value.Number) || Outside(field, value.NumberTo);
            return invalid ? FloxStrings.Message_InvalidValue : null;
        }

        return null;
    }

    private static bool Outside(SearchFormField field, decimal? number)
    {
        if (number is not { } n)
        {
            return false;
        }

        return (decimal.TryParse(field.Min, NumberStyles.Number, CultureInfo.InvariantCulture, out var min) && n < min) || (decimal.TryParse(field.Max, NumberStyles.Number, CultureInfo.InvariantCulture, out var max) && n > max);
    }

    private static bool Matches(string pattern, string text)
    {
        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            // A broken pattern of the form must not block the user
            return true;
        }
    }

    private static string? Comparator(string? comparator)
    {
        return string.IsNullOrWhiteSpace(comparator) || comparator == EqualComparator ? null : comparator;
    }

    private static string? Blank(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
