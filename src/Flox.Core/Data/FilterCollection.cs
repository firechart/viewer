using System.Globalization;
using System.Linq.Expressions;

namespace Flox.Core.Data;

public class FilterCollection : List<Filter>
{
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

    public T? GetValue<T>(string field, T? defaultValue = default)
    {
        var f = this.FirstOrDefault(x => x.Field == field);
        if (f?.Value == null)
        {
            return defaultValue;
        }

        return (T)Convert.ChangeType(f.Value, typeof(T), CultureInfo.CurrentCulture);
    }

    public void ClearValue(string field)
    {
        var f = this.FirstOrDefault(x => x.Field == field);
        if (f != null)
        {
            Remove(f);
        }
    }

    public Filter? this[string field]
    {
        get
        {
            var result = this.FirstOrDefault(x => x.Field == field);
            return result;
        }
    }
}

public class FilterCollection<TEntity> : FilterCollection
{
    public FilterCollection()
    {
    }

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
