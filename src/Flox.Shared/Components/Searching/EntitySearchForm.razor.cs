using FilterOperator = Flox.Core.Data.FilterOperator;

namespace Flox.Shared.Components.Searching;

/// <summary>
/// Which end of a range a search value belongs to.
/// </summary>
public enum RangeBound
{
    /// <summary>
    /// The lower bound of the range.
    /// </summary>
    From,
    /// <summary>
    /// The upper bound of the range.
    /// </summary>
    To
}

/// <summary>
/// A search form generated from the searchable properties of an entity; it builds the filters of a search from the values entered.
/// </summary>
public partial class EntitySearchForm<TEntity>
{
    private readonly Dictionary<string, object?> _values = new Dictionary<string, object?>();

    private List<SearchFieldDescriptor> Fields
    {
        get
        {
            return SearchFieldDescriptorCache.GetFields<TEntity>();
        }
    }

    /// <summary>
    /// CSS class added to the form.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Raised after the form was reset.
    /// </summary>
    [Parameter]
    public EventCallback OnReset { get; set; }

    /// <summary>
    /// Raised with the filters built from the entered values when the user searches.
    /// </summary>
    [Parameter]
    public EventCallback<FilterCollection> OnSearch { get; set; }

    /// <summary>
    /// Whether the form shows its own search and reset buttons.
    /// </summary>
    [Parameter]
    public bool ShowActions { get; set; } = true;

    private static string RangeKey(SearchFieldDescriptor field, RangeBound bound)
    {
        return $"{field.Field}:{bound}";
    }

    /// <summary>
    /// Builds the filters of the entered values.
    /// </summary>
    public FilterCollection BuildFilters()
    {
        FilterCollection filters = new FilterCollection();

        foreach (var field in Fields)
        {
            switch (field.ControlType)
            {
                case SearchControlType.DateRange:
                case SearchControlType.NumberRange:
                    if (_values.TryGetValue(RangeKey(field, RangeBound.From), out var from) && from is not null)
                    {
                        filters.SetValue(field.Field, from, FilterOperator.Gte);
                    }

                    if (_values.TryGetValue(RangeKey(field, RangeBound.To), out var to) && to is not null)
                    {
                        filters.SetValue(field.Field, to, FilterOperator.Lte);
                    }

                    break;

                case SearchControlType.MultiSelect:
                    if (_values.TryGetValue(field.Field, out var values) && values is List<string> { Count: > 0 } list)
                    {
                        filters.SetValue(field.Field, list, FilterOperator.In);
                    }

                    break;

                default:
                    if (_values.TryGetValue(field.Field, out var value) && value is not null)
                    {
                        if (value is string s)
                        {
                            var hasWildcards = s.Contains('*', StringComparison.Ordinal);
                            if (hasWildcards || field.Operator == FilterOperator.Like)
                            {
                                // Plain text (no wildcards) is wrapped as "*value*" for a case-insensitive "contains" match
                                var pattern = hasWildcards ? s : $"*{s}*";
                                filters.SetValue(field.Field, pattern, FilterOperator.Like);
                            }
                            else
                            {
                                filters.SetValue(field.Field, s, field.Operator);
                            }
                        }
                        else
                        {
                            filters.SetValue(field.Field, value, field.Operator);
                        }
                    }

                    break;
            }
        }

        return filters;
    }

    /// <summary>
    /// Fills the form with the values of the filters.
    /// </summary>
    public void LoadFilters(FilterCollection filters)
    {
        _values.Clear();

        foreach (var field in Fields)
        {
            switch (field.ControlType)
            {
                case SearchControlType.DateRange:
                case SearchControlType.NumberRange:
                    var from = filters.FirstOrDefault(f => f.Field == field.Field && f.Operator == FilterOperator.Gte);
                    if (from?.Value is not null)
                    {
                        SetRangeValue(field, RangeBound.From, from.Value);
                    }

                    var to = filters.FirstOrDefault(f => f.Field == field.Field && f.Operator == FilterOperator.Lte);
                    if (to?.Value is not null)
                    {
                        SetRangeValue(field, RangeBound.To, to.Value);
                    }

                    break;

                case SearchControlType.MultiSelect:
                    var inFilter = filters.FirstOrDefault(f => f.Field == field.Field && f.Operator == FilterOperator.In);
                    if (inFilter?.Value is List<string> values)
                    {
                        SetValues(field, values);
                    }

                    break;

                default:
                    var single = filters.FirstOrDefault(f => f.Field == field.Field);
                    if (single?.Value is string sv)
                    {
                        // Unwrap the implicit "*value*" pattern back to plain text for editing
                        if (single.Operator == FilterOperator.Like && sv.Length >= 2 && sv[0] == '*' && sv[^1] == '*' && sv[1..^1] is var inner && !inner.Contains('*', StringComparison.Ordinal))
                        {
                            SetValue(field, inner);
                        }
                        else
                        {
                            SetValue(field, sv);
                        }
                    }
                    else if (single?.Value is not null)
                    {
                        SetValue(field, single.Value);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Clears all entered values.
    /// </summary>
    public void Reset()
    {
        _values.Clear();
    }

    // Any, then yes, then no, then any again
    private static bool? NextBool(bool? value)
    {
        return value switch
        {
            null => true,
            true => false,
            false => null
        };
    }

    private static string? NumberText(double? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static double? ParseNumber(string? text)
    {
        return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private bool? GetBool(SearchFieldDescriptor field)
    {
        return _values.GetValueOrDefault(field.Field) as bool?;
    }

    private DateTime? GetDate(SearchFieldDescriptor field, RangeBound bound)
    {
        return _values.GetValueOrDefault(RangeKey(field, bound)) as DateTime?;
    }

    private double? GetNumber(SearchFieldDescriptor field, RangeBound bound)
    {
        return _values.GetValueOrDefault(RangeKey(field, bound)) as double?;
    }

    private string? GetString(SearchFieldDescriptor field)
    {
        return _values.GetValueOrDefault(field.Field) as string;
    }

    private IReadOnlyCollection<string> GetStrings(SearchFieldDescriptor field)
    {
        return _values.GetValueOrDefault(field.Field) as List<string> ?? [];
    }

    private async Task ResetAsync()
    {
        Reset();
        await OnReset.InvokeAsync();
    }

    private async Task SearchAsync()
    {
        await OnSearch.InvokeAsync(BuildFilters());
    }

    private void SetRangeValue(SearchFieldDescriptor field, RangeBound bound, object? value)
    {
        var key = RangeKey(field, bound);
        if (value is null)
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value;
        }
    }

    private void SetValue(SearchFieldDescriptor field, object? value)
    {
        if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
        {
            _values.Remove(field.Field);
        }
        else
        {
            _values[field.Field] = value;
        }
    }

    private void SetValues(SearchFieldDescriptor field, IEnumerable<string> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            _values.Remove(field.Field);
        }
        else
        {
            _values[field.Field] = list;
        }
    }
}
