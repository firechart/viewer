using FilterOperator = Flox.Core.Data.FilterOperator;

namespace Flox.Shared.Components.Searching;

public enum RangeBound
{
    From,
    To
}

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

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public EventCallback OnReset { get; set; }

    [Parameter]
    public EventCallback<FilterCollection> OnSearch { get; set; }

    [Parameter]
    public bool ShowActions { get; set; } = true;

    private static string RangeKey(SearchFieldDescriptor field, RangeBound bound)
    {
        return $"{field.Field}:{bound}";
    }

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

    public void Reset()
    {
        _values.Clear();
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
