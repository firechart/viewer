using System.Linq.Expressions;

namespace Flox.Core.Data;

/// <summary>How a filter compares a field with its value, or combines nested filters.</summary>
public enum FilterOperator
{
    /// <summary>No operation.</summary>
    None,
    /// <summary>All nested filters hold.</summary>
    And,
    /// <summary>Any nested filter holds.</summary>
    Or,
    /// <summary>Equal.</summary>
    Eq,
    /// <summary>Not equal.</summary>
    Ne,
    /// <summary>The value is one of a list.</summary>
    In,
    /// <summary>Greater than.</summary>
    Gt,
    /// <summary>Greater than or equal.</summary>
    Gte,
    /// <summary>Less than.</summary>
    Lt,
    /// <summary>Less than or equal.</summary>
    Lte,

    /// <summary>The text contains the value.</summary>
    Contains,
    /// <summary>The text starts with the value.</summary>
    StartsWith,
    /// <summary>The text starts with the value, ignoring case.</summary>
    StartsWithIgnoreCase,
    /// <summary>Equal, ignoring case.</summary>
    EqualsIgnoreCase,
    /// <summary>Equal to any of a list of values, ignoring case.</summary>
    AnyOfIgnoreCase,

    /// <summary>A case-insensitive wildcard pattern (an asterisk is any sequence of characters at the start or end).</summary>
    Like,

    /// <summary>For lists and arrays: any element matches.</summary>
    Any
}

/// <summary>One condition of a request: a field, an operator and a value, or an expression, or nested filters.</summary>
public class Filter
{
    /// <summary>The name of the field to compare.</summary>
    public string Field { get; set; } = null!;
    /// <summary>
    /// The nested filters, combined by <see cref="Operator"/> when it is <see cref="FilterOperator.And"/> or <see cref="FilterOperator.Or"/>.
    /// </summary>
    public FilterCollection Filters { get; } = [];
    /// <summary>Tells whether the filter is applied by the client after the load rather than by the storage.</summary>
    public bool IsClient { get; set; }
    /// <summary>The operator of the filter.</summary>
    public FilterOperator Operator { get; set; } = FilterOperator.None;
    /// <summary>The value to compare with.</summary>
    public object? Value { get; set; }
    /// <summary>A lambda expression to apply instead of the field comparison.</summary>
    public LambdaExpression? Expression { get; set; }
}
