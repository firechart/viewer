using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.WebUtilities;

namespace Flox.Shared.Components;

public class UrlStateBuilder : Dictionary<string, List<object>>
{
    public UrlStateBuilder(NavigationManager navigationManager) : base(StringComparer.OrdinalIgnoreCase)
    {
        _navigationManager = navigationManager;
    }

    private readonly NavigationManager _navigationManager;

    private void InternalAdd(string parameterName, object? value)
    {
        if (value == null)
        {
            return;
        }

        if (ContainsKey(parameterName))
        {
            var l = this[parameterName];
            l.Add(value);
        }
        else
        {
            var l = new List<object>
            {
                value
            };
            base.Add(parameterName, l);
        }
    }

    public UrlStateBuilder Add(Expression<Func<object?>> expr)
    {
        var name = expr.Body switch
        {
            MemberExpression m => m.Member.Name,
            UnaryExpression { Operand: MemberExpression m } => m.Member.Name,
            var _ => throw new ArgumentException("Expression must be a property accessor", nameof(expr))
        };
        var value = expr.Compile()
                        .Invoke();
        if (value != null)
        {
            InternalAdd(name, value);
        }

        return this;
    }

    public UrlStateBuilder Add(string parameterName, Expression<Func<object?>> expr)
    {
        var name = expr.Body switch
        {
            MemberExpression m => m.Member.Name,
            UnaryExpression { Operand: MemberExpression m } => m.Member.Name,
            var _ => throw new ArgumentException("Expression must be a property accessor", nameof(expr))
        };
        var value = expr.Compile()
                        .Invoke();
        if (value != null)
        {
            InternalAdd(parameterName, value);
        }

        return this;
    }

    public UrlStateBuilder Add(string parameterName, object? value)
    {
        InternalAdd(parameterName, value);
        return this;
    }

    public string ToUrl()
    {
        var uri = new Uri(_navigationManager.Uri).GetLeftPart(UriPartial.Path);
        foreach (var kv in this)
        {
            var val = kv.Value;
            foreach (var v in val)
            {
                if (v == null)
                {
                    continue;
                }

                if (v is string str && string.IsNullOrEmpty(str))
                {
                    continue;
                }

                if (v is bool b && !b)
                {
                    continue;
                }

                if (v != null)
                {
                    uri = QueryHelpers.AddQueryString(uri, kv.Key, Convert.ToString(v, CultureInfo.CurrentCulture)!);
                }
            }
        }

        return uri;
    }

    public void Navigate()
    {
        var url = ToUrl();
        _navigationManager.NavigateTo(url, false);
    }
}
