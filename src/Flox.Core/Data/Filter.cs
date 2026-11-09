using System.Linq.Expressions;

namespace Flox.Core.Data;

public enum FilterOperator
{
    None,
    And,
    Or,
    Eq,
    Ne,
    In,
    Gt,
    Gte,
    Lt,
    Lte,

    Contains,
    StartsWith,
    StartsWithIgnoreCase,
    EqualsIgnoreCase,
    AnyOfIgnoreCase,

    // Case-insensitive wildcard pattern ('*' = any sequence of characters at the start/end)
    Like,

    // For lists and arrays
    Any
}

public class Filter
{
    public string Field { get; set; } = null!;
    public FilterCollection Filters { get; } = [];
    public bool IsClient { get; set; }
    public FilterOperator Operator { get; set; } = FilterOperator.None;
    public object? Value { get; set; }
    public LambdaExpression? Expression { get; set; }
}
