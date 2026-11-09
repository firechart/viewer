using System.Linq.Expressions;

namespace Flox.Core.Data;

public class SortCollection : List<Sort>
{
    public void SetValue(string field, bool ascending = true)
    {
        var s = Find(s => s.Field == field);
        if (s == null)
        {
            s = new Sort();
            s.Field = field;
            Add(s);
        }

        s.Ascending = ascending;
    }

    public void ClearValue(string field)
    {
        var s = Find(s => s.Field == field);
        if (s != null)
        {
            Remove(s);
        }
    }
}

public class SortCollection<TEntity> : SortCollection
{
    public SortCollection()
    {
    }

    public void SetValue(Expression<Func<TEntity, object>> expression, bool ascending = true)
    {
        var s = Find(row => row.Expression != null && row.Expression.ToString() == expression.ToString());
        if (s == null)
        {
            s = new Sort
            {
                Expression = expression
            };
            Add(s);
        }

        s.Expression = expression;
        s.Ascending = ascending;
    }

    public void ClearValue(Expression<TEntity> expression)
    {
        var s = Find(row => row.Expression != null && row.Expression.ToString() == expression.ToString());
        if (s != null)
        {
            Remove(s);
        }
    }
}
