using System.Collections;
using System.Globalization;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace Flox.Core.Data;

public static class PagedRequestTranslator
{
    // A top of zero or less is no limit; the skip applies either way
    public static IQueryable<T> ApplyPaging<T>(IQueryable<T> query, int skip, int top)
    {
        if (skip > 0)
        {
            query = query.Skip(skip);
        }

        return top > 0 ? query.Take(top) : query;
    }

    public static IQueryable<T> ApplySorting<T>(IQueryable<T> query, List<Sort> sorts)
    {
        if (sorts.Count == 0)
        {
            return query;
        }

        // Typed key selectors, not a Dynamic LINQ string: LINQ providers such as Marten's do not take the latter, and a field
        // name that is not a property of T fails here instead of being parsed as an expression
        IOrderedQueryable<T>? ordered = null;
        foreach (var sort in sorts)
        {
            var parameter = Expression.Parameter(typeof(T), "row");
            Expression key = parameter;
            foreach (var part in sort.Field.Split('.'))
            {
                key = Expression.PropertyOrField(key, part);
            }

            var method = ordered is null ? (sort.Ascending ? nameof(Queryable.OrderBy) : nameof(Queryable.OrderByDescending)) : (sort.Ascending ? nameof(Queryable.ThenBy) : nameof(Queryable.ThenByDescending));
            var source = ordered ?? query;
            var call = Expression.Call(typeof(Queryable), method, [typeof(T), key.Type], source.Expression, Expression.Quote(Expression.Lambda(key, parameter)));
            ordered = (IOrderedQueryable<T>)source.Provider.CreateQuery<T>(call);
        }

        return ordered!;
    }

    public static Expression<Func<T, bool>>? BuildFilterExpression<T>(List<Filter> filters)
    {
        if (filters.Count == 0)
        {
            return null;
        }

        var parameter = Expression.Parameter(typeof(T), "entity");
        Expression? combined = null;

        foreach (var filter in filters)
        {
            var part = BuildExpression<T>(filter, parameter);
            if (part != null)
            {
                combined = combined == null ? part : Expression.AndAlso(combined, part);
            }
        }

        return combined == null ? null : Expression.Lambda<Func<T, bool>>(combined, parameter);
    }

    private static Expression? BuildExpression<T>(Filter filter, ParameterExpression parameter)
    {
        if (filter.Expression != null)
        {
            var visitor = new ParameterReplacerVisitor(filter.Expression.Parameters[0], parameter);
            return visitor.Visit(filter.Expression.Body);
        }

        return filter.Operator switch
        {
            FilterOperator.And => BuildComposite<T>(filter.Filters, parameter, Expression.AndAlso),
            FilterOperator.Or => BuildComposite<T>(filter.Filters, parameter, Expression.OrElse),
            FilterOperator.In => BuildInExpression(filter, parameter),
            FilterOperator.Any => BuildAnyExpression(filter, parameter),
            var _ => BuildDynamic<T>(filter, parameter)
        };
    }

    private static Expression? BuildComposite<T>(FilterCollection children, ParameterExpression parameter, Func<Expression, Expression, Expression> combine)
    {
        Expression? result = null;
        foreach (var child in children)
        {
            var part = BuildExpression<T>(child, parameter);
            if (part != null)
            {
                result = result == null ? part : combine(result, part);
            }
        }

        return result;
    }

    private static Expression? BuildInExpression(Filter filter, ParameterExpression parameter)
    {
        if (filter.Value is not IEnumerable enumerable)
        {
            return null;
        }

        var property = Expression.Property(parameter, filter.Field);
        var values = enumerable.Cast<object>()
                               .ToList();

        if (values.Count == 0)
        {
            return Expression.Constant(false);
        }

        Expression? result = null;
        foreach (var value in values)
        {
            var constant = Expression.Constant(value, property.Type);
            var eq = Expression.Equal(property, constant);
            result = result == null ? eq : Expression.OrElse(result, eq);
        }

        return result;
    }

    private static Expression? BuildAnyExpression(Filter filter, ParameterExpression parameter)
    {
        var property = Expression.Property(parameter, filter.Field);
        var propertyType = property.Type;

        if (!propertyType.IsGenericType)
        {
            return null;
        }

        var elementType = propertyType.GetGenericArguments()[0];
        var anyMethod = typeof(Enumerable).GetMethods()
                                          .First(m => m.Name == nameof(Enumerable.Any) && m.GetParameters()
                                                                                           .Length == 2)
                                          .MakeGenericMethod(elementType);
        var itemParameter = Expression.Parameter(elementType, "item");

        if (filter.Value is IEnumerable enumerable and not string)
        {
            var values = enumerable.Cast<object>()
                                   .Select(v => Convert.ChangeType(v, elementType, CultureInfo.CurrentCulture))
                                   .ToList();

            if (values.Count == 0)
            {
                return Expression.Constant(false);
            }

            var listType = typeof(List<>).MakeGenericType(elementType);
            var typedList = Activator.CreateInstance(listType)!;
            var addMethod = listType.GetMethod("Add")!;
            foreach (var value in values)
            {
                addMethod.Invoke(typedList, [value]);
            }

            var valuesConstant = Expression.Constant(typedList);
            var containsMethod = typeof(Enumerable).GetMethods()
                                                   .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters()
                                                                                                         .Length == 2)
                                                   .MakeGenericMethod(elementType);
            var containsCall = Expression.Call(null, containsMethod, valuesConstant, itemParameter);
            return Expression.Call(null, anyMethod, property, Expression.Lambda(containsCall, itemParameter));
        }
        else
        {
            var converted = Convert.ChangeType(filter.Value, elementType, CultureInfo.CurrentCulture);
            var itemEquals = Expression.Equal(itemParameter, Expression.Constant(converted, elementType));
            return Expression.Call(null, anyMethod, property, Expression.Lambda(itemEquals, itemParameter));
        }
    }

    private static Expression? BuildDynamic<T>(Filter filter, ParameterExpression parameter)
    {
        (var pred, var args) = ToPredicate(filter);
        if (pred == null)
        {
            return null;
        }

        return DynamicExpressionParser.ParseLambda([parameter], typeof(bool), pred, args)
                                      .Body;
    }

    // Converts a scalar filter to a Dynamic LINQ predicate string + parameter values.
    // 'And', 'Or', 'In', 'Any' are handled before reaching this point.
    private static (string? pred, object?[] args) ToPredicate(Filter f)
    {
        var p = new List<object?>();
        var pred = f.Operator switch
        {
            FilterOperator.Eq => Param(p, f.Value, i => $"{f.Field} == @{i}"),
            FilterOperator.Ne => Param(p, f.Value, i => $"{f.Field} != @{i}"),
            FilterOperator.Gt => Param(p, f.Value, i => $"{f.Field} > @{i}"),
            FilterOperator.Gte => Param(p, f.Value, i => $"{f.Field} >= @{i}"),
            FilterOperator.Lt => Param(p, f.Value, i => $"{f.Field} < @{i}"),
            FilterOperator.Lte => Param(p, f.Value, i => $"{f.Field} <= @{i}"),
            FilterOperator.Contains when f.Value is string => Param(p, f.Value, i => $"{f.Field} != null && {f.Field}.Contains(@{i})"),
            FilterOperator.StartsWith when f.Value is string => Param(p, f.Value, i => $"{f.Field} != null && {f.Field}.StartsWith(@{i})"),
            FilterOperator.StartsWithIgnoreCase when f.Value is string sv => Param(p, sv.ToLower(CultureInfo.CurrentCulture), i => $"{f.Field} != null && {f.Field}.ToLower().StartsWith(@{i})"),
            FilterOperator.EqualsIgnoreCase when f.Value is string sv => Param(p, sv.ToLower(CultureInfo.CurrentCulture), i => $"{f.Field} != null && {f.Field}.ToLower() == @{i}"),
            FilterOperator.Like => BuildLikePred(f, p),
            var _ => null
        };
        return (pred, [.. p]);
    }

    private static string Param(List<object?> p, object? value, Func<int, string> fmt)
    {
        var i = p.Count;
        p.Add(value);
        return fmt(i);
    }

    // Wildcard pattern: '*' at start/end maps to Contains/StartsWith/EndsWith/Equals (case-insensitive).
    private static string? BuildLikePred(Filter f, List<object?> p)
    {
        if (f.Value is not string pattern)
        {
            return null;
        }

        var sw = pattern.StartsWith('*');
        var ew = pattern.EndsWith('*');
        var value = pattern.Trim('*')
                           .ToLower(CultureInfo.CurrentCulture);
        var method = (sw, ew) switch
        {
            (true, true) => "Contains",
            (false, true) => "StartsWith",
            (true, false) => "EndsWith",
            var _ => null
        };
        return Param(p, value, i => method is not null ? $"{f.Field} != null && {f.Field}.ToLower().{method}(@{i})" : $"{f.Field} != null && {f.Field}.ToLower() == @{i}");
    }

    private class ParameterReplacerVisitor
    (
        ParameterExpression oldParam,
        ParameterExpression newParam
    ) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == oldParam ? newParam : base.VisitParameter(node);
        }
    }
}
