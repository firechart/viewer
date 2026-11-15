using System.Linq.Expressions;

namespace Flox.Core.Data;

/// <summary>A list of sorts addressed by the name of their field.</summary>
public class SortCollection : List<Sort>
{
    /// <summary>Sets the direction of the sort of the field, adding it when there is none.</summary>
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

    /// <summary>Removes the sort of the field.</summary>
    public void ClearValue(string field)
    {
        var s = Find(s => s.Field == field);
        if (s != null)
        {
            Remove(s);
        }
    }
}

/// <summary>A collection of sorts that also takes expressions over the entity.</summary>
public class SortCollection<TEntity> : SortCollection
{
    /// <summary>Creates an empty collection.</summary>
    public SortCollection()
    {
    }

    /// <summary>Sets the direction of the sort by the expression, adding it when there is none.</summary>
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

    /// <summary>Removes the sort by the expression.</summary>
    public void ClearValue(Expression<TEntity> expression)
    {
        var s = Find(row => row.Expression != null && row.Expression.ToString() == expression.ToString());
        if (s != null)
        {
            Remove(s);
        }
    }
}
