using System.Globalization;
using System.Linq.Expressions;

namespace Flox.Core.Data;

/// <summary>A list of filters addressed by the name of their field.</summary>
public class FilterCollection : List<Filter>
{
    /// <summary>Sets the filter of the field, adding it when there is none.</summary>
    public FilterCollection SetValue(string field, object value, FilterOperator op = FilterOperator.Eq)
    {
        var f = this[field];
        if (f == null)
        {
            Add(new Filter
            {
                Field = field,
                Value = value,
                Operator = op
            });
        }
        else
        {
            f.Value = value;
            f.Operator = op;
        }

        return this;
    }

    /// <summary>Sets the filter of the field, adding it when there is none.</summary>
    public FilterCollection SetValue<T>(string field, T value, FilterOperator op = FilterOperator.Eq)
    {
        var f = this[field];
        if (f == null)
        {
            Add(new Filter
            {
                Field = field,
                Value = value,
                Operator = op
            });
        }
        else
        {
            f.Value = value;
            f.Operator = op;
        }

        return this;
    }

    /// <summary>Returns the value of the filter of the field converted to the type, or the default when there is none.</summary>
    public T? GetValue<T>(string field, T? defaultValue = default)
    {
        var f = this.FirstOrDefault(x => x.Field == field);
        if (f?.Value == null)
        {
            return defaultValue;
        }

        return (T)Convert.ChangeType(f.Value, typeof(T), CultureInfo.CurrentCulture);
    }

    /// <summary>Removes the filter of the field.</summary>
    public void ClearValue(string field)
    {
        var f = this.FirstOrDefault(x => x.Field == field);
        if (f != null)
        {
            Remove(f);
        }
    }

    /// <summary>The filter of the field, or null when there is none.</summary>
    public Filter? this[string field]
    {
        get
        {
            var result = this.FirstOrDefault(x => x.Field == field);
            return result;
        }
    }
}

/// <summary>A collection of filters that also takes expressions over the entity.</summary>
public class FilterCollection<TEntity> : FilterCollection
{
    /// <summary>Creates an empty collection.</summary>
    public FilterCollection()
    {
    }

    /// <summary>Adds the expression as a filter unless the same instance is already there.</summary>
    public void Add(Expression<Func<TEntity, bool>> expression)
    {
        var f = this.FirstOrDefault(row => row.Expression == expression);
        if (f == null)
        {
            Add(new Filter
            {
                Expression = expression
            });
        }
    }

    /// <summary>Sets the expression as a filter, replacing one that has the same text.</summary>
    public void SetValue(Expression<Func<TEntity, bool>> expression)
    {
        var f = this.FirstOrDefault(row => row.Expression != null && row.Expression.ToString() == expression.ToString());
        if (f == null)
        {
            Add(new Filter
            {
                Expression = expression
            });
        }
        else
        {
            f.Expression = expression;
        }
    }
}
