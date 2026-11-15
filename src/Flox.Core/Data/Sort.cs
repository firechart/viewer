using System.Linq.Expressions;

namespace Flox.Core.Data;

/// <summary>One sort criterion of a request: a field and a direction, or an expression.</summary>
public class Sort
{
    private LambdaExpression? _expression;

    /// <summary>The name of the field to sort by; taken from <see cref="Expression"/> when not set.</summary>
    public string Field { get; set; } = null!;
    /// <summary>Tells whether the order is ascending.</summary>
    public bool Ascending { get; set; } = true;

    /// <summary>A lambda expression that picks the property to sort by.</summary>
    public LambdaExpression? Expression
    {
        get
        {
            return _expression;
        }

        set
        {
            _expression = value;
            if (value is not null)
            {
                Field ??= ExtractPropertyName(value)!;
            }
        }
    }

    private static string? ExtractPropertyName(LambdaExpression expr)
    {
        var body = expr.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } u)
        {
            body = u.Operand;
        }

        return body is MemberExpression m ? m.Member.Name : null;
    }
}
