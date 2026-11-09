using System.Linq.Expressions;

namespace Flox.Core.Data;

public class Sort
{
    private LambdaExpression? _expression;

    public string Field { get; set; } = null!;
    public bool Ascending { get; set; } = true;

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
