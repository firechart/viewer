using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.WebUtilities;

namespace Flox.Shared.Components;

/// <summary>
/// Collects the values of the state of a page and builds the URL of the current page with them as a query string.
/// </summary>
public class UrlStateBuilder : Dictionary<string, List<object>>
{
    /// <summary>
    /// Creates a builder for the current page of the navigation manager.
    /// </summary>
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

    /// <summary>
    /// Adds the value of the property the expression reads, under the name of the property; null is skipped.
    /// </summary>
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

    /// <summary>
    /// Adds the value the expression reads under the given parameter name; null is skipped.
    /// </summary>
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

    /// <summary>
    /// Adds the value under the given parameter name; null is skipped.
    /// </summary>
    public UrlStateBuilder Add(string parameterName, object? value)
    {
        InternalAdd(parameterName, value);
        return this;
    }

    /// <summary>
    /// Builds the URL of the current page with the added values as a query string; empty strings and false are left out.
    /// </summary>
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

    /// <summary>
    /// Navigates to the URL built by <see cref="ToUrl"/>.
    /// </summary>
    public void Navigate()
    {
        var url = ToUrl();
        _navigationManager.NavigateTo(url, false);
    }
}
