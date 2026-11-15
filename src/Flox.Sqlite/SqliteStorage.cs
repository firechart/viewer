using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Flox.Core.Data;

namespace Flox.Sqlite;

/// <summary>
/// IStorage over a base of an ISqliteEngine, read only, by a mapping. A request is SQL as far as it goes: the filters of the columns (=, &lt;&gt;, IN, &lt;, >,
/// starts with, contains, ignoring the case, wildcards) are the WHERE, the full-text search is FTS5, the sorts of the columns are the ORDER BY and the
/// window is LIMIT/OFFSET. What is not SQL (an expression, a property without a column) is done in memory over the rows the SQL found, with the rules
/// of the other storages (PagedRequestTranslator). A base that is not there reads as empty.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class SqliteStorage<T> : IGuidEntityStorage<T> where T : class, IGuidEntity
{
    private readonly SqliteSource _source;
    private readonly SqliteMapping<T> _table;

    /// <summary>
    /// Creates a storage over a source and the mapping of the entity.
    /// </summary>
    /// <param name="source">The base to read.</param>
    /// <param name="mapping">How the entity is read from the base.</param>
    public SqliteStorage(SqliteSource source, SqliteMapping<T> mapping)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _table = mapping ?? throw new ArgumentNullException(nameof(mapping));
    }

    /// <inheritdoc />
    public Task<PagedResult<T>> FindAsync(PagedRequest<T> request, CancellationToken cancellationToken = default)
    {
        return FindAsync(request, false, cancellationToken);
    }

    // countOnly: the total alone, no row read when SQL can count
    private async Task<PagedResult<T>> FindAsync(PagedRequest<T> request, bool countOnly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = new PagedResult<T>();
        if (await _source.DatabaseAsync(cancellationToken) is not { } database)
        {
            result.Total = request.ReturnTotal ? 0 : null;
            return result;
        }

        var plan = Plan(request);
        var withData = new SqliteRead(!countOnly && (request.Fields.Count == 0 || request.Fields.Any(_table.LargeFields.Contains)));
        var inSql = plan.Residual.Count == 0 && plan.SortsInSql && (plan.FullText is null || _table.FullText is not null);
        if (inSql)
        {
            if (request.ReturnTotal)
            {
                var count = Query(plan, "count(*)", string.Empty, 0, 0);
                result.Total = Convert.ToInt64((await database.QueryAsync(count.Sql, count.Parameters, cancellationToken)).Scalar(), CultureInfo.InvariantCulture);
                if (countOnly)
                {
                    return result;
                }
            }

            var page = Query(plan, _table.Select, Order(request.Sorts), request.Skip, request.Top);
            result.Items.AddRange((await database.QueryAsync(page.Sql, page.Parameters, cancellationToken)).Rows.Select(row => _table.Read(row, withData)));
            return result;
        }

        var all = Query(plan, _table.Select, $"ORDER BY {_table.RowId}", 0, 0);
        IEnumerable<T> matches = (await database.QueryAsync(all.Sql, all.Parameters, cancellationToken)).Rows.Select(row => _table.Read(row, withData)).ToList();
        if (PagedRequestTranslator.BuildFilterExpression<T>(plan.Residual) is { } residual)
        {
            matches = matches.Where(residual.Compile());
        }

        if (plan.FullText is not null && _table.FullText is null)
        {
            matches = matches.Where(row => MatchesText(row, plan.FullText));
        }

        var list = (request.Sorts.Count > 0 ? SortInMemory(matches, request.Sorts) : matches).ToList();
        result.Total = request.ReturnTotal ? list.Count : null;
        IEnumerable<T> window = list;
        if (request.Skip > 0)
        {
            window = window.Skip(request.Skip);
        }

        if (request.Top > 0)
        {
            window = window.Take(request.Top);
        }

        result.Items.AddRange(window);
        return result;
    }

    /// <inheritdoc />
    public async Task<T?> GetByIdAsync(Guid key, CancellationToken cancellationToken = default)
    {
        if (!_table.Addressable || SqliteKeys.Parse(key) is not { } parsed || parsed.Table != _table.Table
            || await _source.DatabaseAsync(cancellationToken) is not { } database)
        {
            return null;
        }

        var rows = await database.QueryAsync($"SELECT {_table.Select} FROM {_table.From} WHERE {_table.RowId} = $key",
                                             new Dictionary<string, object?> { ["$key"] = parsed.RowId }, cancellationToken);
        return rows.Count == 0 ? null : _table.Read(rows[0], new SqliteRead(true));
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid key, CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(key, cancellationToken) is not null;
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var request = new PagedRequest<T> { PageSize = 0, ReturnTotal = true };
        if (predicate is not null)
        {
            request.Filters.Add(new Filter { Expression = predicate });
        }

        return (await FindAsync(request, true, cancellationToken)).Total ?? 0;
    }

    /// <inheritdoc />
    /// <remarks>The filters of the columns are SQL: count(*) without reading a row.</remarks>
    public async Task<long> CountAsync(FilterCollection<T> filters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var request = new PagedRequest<T> { PageSize = 0, ReturnTotal = true };
        request.Filters.AddRange(filters);
        return (await FindAsync(request, true, cancellationToken)).Total ?? 0;
    }

    /// <inheritdoc />
    /// <remarks>Every row is read only.</remarks>
    public Task<IReadOnlyList<EntityCapability<Guid>>> GetCapabilitiesAsync(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return Task.FromResult<IReadOnlyList<EntityCapability<Guid>>>([.. keys.Distinct().Select(key => new EntityCapability<Guid>(key, false, false, false))]);
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: the storage is read only.</exception>
    public Task<T> InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        throw ReadOnly();
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: the storage is read only.</exception>
    public Task<T> UpdateAsync(Guid key, T value, CancellationToken cancellationToken = default)
    {
        throw ReadOnly();
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: the storage is read only.</exception>
    public Task DeleteAsync(Guid key, CancellationToken cancellationToken = default)
    {
        throw ReadOnly();
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: the storage is read only.</exception>
    public Task<long> DeleteManyAsync(Expression<Func<T, bool>> expression, int limit = Flox.Core.Constants.MAX_BATCH_DELETE, CancellationToken cancellationToken = default)
    {
        throw ReadOnly();
    }

    private static NotSupportedException ReadOnly()
    {
        return new NotSupportedException($"The SQLite base of {typeof(T).Name} is read only.");
    }

    // The filters of SQL and the rest
    private QueryPlan Plan(PagedRequest<T> request)
    {
        var sql = new List<Filter>();
        var residual = new List<Filter>();
        foreach (var filter in request.Filters)
        {
            if (filter.Expression is null && filter.Field is not null && _table.Columns.ContainsKey(filter.Field) && Translatable(filter))
            {
                sql.Add(filter);
            }
            else
            {
                residual.Add(filter);
            }
        }

        var sortsInSql = request.Sorts.All(sort => sort.Field is not null && _table.Columns.ContainsKey(sort.Field));
        var text = string.IsNullOrWhiteSpace(request.FullTextSearchString) ? null : request.FullTextSearchString.Trim();
        return new QueryPlan(sql, residual, text, sortsInSql);
    }

    private static bool Translatable(Filter filter)
    {
        return filter.Operator switch
        {
            FilterOperator.Eq or FilterOperator.Ne => true,
            FilterOperator.Gt or FilterOperator.Gte or FilterOperator.Lt or FilterOperator.Lte => filter.Value is not null,
            FilterOperator.In => filter.Value is IEnumerable and not string,
            FilterOperator.Contains or FilterOperator.StartsWith or FilterOperator.StartsWithIgnoreCase or FilterOperator.EqualsIgnoreCase or FilterOperator.Like
                => filter.Value is string,
            _ => false
        };
    }

    // SELECT <what> FROM <tables> WHERE <the filters of SQL> <order> <window>, with its parameters
    private SqlQuery Query(QueryPlan plan, string what, string order, int skip, int top)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var parts = new List<string>();
        foreach (var filter in plan.Sql)
        {
            parts.Add(Condition(parameters, _table.Columns[filter.Field], filter));
        }

        if (plan.FullText is not null && _table.FullText is not null)
        {
            parameters["$fts"] = FtsQuery(plan.FullText);
            parts.Add(_table.FullText);
        }

        var where = parts.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", parts);
        var window = top > 0 ? $" LIMIT {top.ToString(CultureInfo.InvariantCulture)} OFFSET {skip.ToString(CultureInfo.InvariantCulture)}"
            : skip > 0 ? $" LIMIT -1 OFFSET {skip.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        return new SqlQuery($"SELECT {what} FROM {_table.From}{where} {order}{window}", parameters);
    }

    private string Order(IReadOnlyList<Sort> sorts)
    {
        var parts = sorts.Select(sort => $"{_table.Columns[sort.Field].Sql} {(sort.Ascending ? "ASC" : "DESC")}").Append(_table.RowId);
        return "ORDER BY " + string.Join(", ", parts);
    }

    private static string Condition(Dictionary<string, object?> parameters, SqliteColumn column, Filter filter)
    {
        var sql = column.Sql;
        object? Value(object? value)
        {
            return column.Value is null ? value : column.Value(value);
        }

        string Parameter(object? value)
        {
            var name = "$p" + parameters.Count.ToString(CultureInfo.InvariantCulture);
            parameters[name] = value switch
            {
                int number => (long)number,
                short number => (long)number,
                bool flag => flag ? 1L : 0L,
                Guid key => key.ToString(),
                Enum named => named.ToString(),
                _ => value
            };
            return name;
        }

        switch (filter.Operator)
        {
            case FilterOperator.Eq:
                return filter.Value is null ? $"{sql} IS NULL" : $"{sql} = {Parameter(Value(filter.Value))}";
            case FilterOperator.Ne:
                return filter.Value is null ? $"{sql} IS NOT NULL" : $"({sql} IS NULL OR {sql} <> {Parameter(Value(filter.Value))})";
            case FilterOperator.Gt:
                return $"{sql} > {Parameter(Value(filter.Value))}";
            case FilterOperator.Gte:
                return $"{sql} >= {Parameter(Value(filter.Value))}";
            case FilterOperator.Lt:
                return $"{sql} < {Parameter(Value(filter.Value))}";
            case FilterOperator.Lte:
                return $"{sql} <= {Parameter(Value(filter.Value))}";
            case FilterOperator.In:
                var values = ((IEnumerable)filter.Value!).Cast<object?>().Select(v => Parameter(Value(v))).ToList();
                return values.Count == 0 ? "0" : $"{sql} IN ({string.Join(", ", values)})";
            case FilterOperator.Contains:
                return $"instr({sql}, {Parameter(filter.Value)}) > 0";
            case FilterOperator.StartsWith:
                var prefix = Parameter(filter.Value);
                return $"substr({sql}, 1, length({prefix})) = {prefix}";
            case FilterOperator.StartsWithIgnoreCase:
                var start = Parameter(filter.Value);
                return $"lower(substr({sql}, 1, length({start}))) = lower({start})";
            case FilterOperator.EqualsIgnoreCase:
                return $"lower({sql}) = lower({Parameter(filter.Value)})";
            case FilterOperator.Like:
                var pattern = (string)filter.Value!;
                var body = new StringBuilder();
                foreach (var c in pattern.Trim('*'))
                {
                    body.Append(c is '%' or '_' or '\\' ? "\\" + c : c.ToString());
                }

                return $"{sql} LIKE {Parameter((pattern.StartsWith('*') ? "%" : string.Empty) + body + (pattern.EndsWith('*') ? "%" : string.Empty))} ESCAPE '\\'";
            default:
                throw new InvalidOperationException($"The filter {filter.Operator} is not SQL.");
        }
    }

    // Every word of the search, as a prefix: "blood pressure" finds "Blood pressure panel"
    private static string FtsQuery(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(word => "\"" + word.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"*"));
    }

    private static bool MatchesText(T row, string text)
    {
        var values = typeof(T).GetProperties().Where(p => p.PropertyType == typeof(string)).Select(p => p.GetValue(row) as string).OfType<string>().ToList();
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                   .All(word => values.Any(value => value.Contains(word, StringComparison.CurrentCultureIgnoreCase)));
    }

    /// <summary>
    /// Sorts rows in memory by the sorts of a request (the rows of several bases, what SQL could not sort).
    /// </summary>
    public static IOrderedEnumerable<T> SortInMemory(IEnumerable<T> rows, IReadOnlyList<Sort> sorts)
    {
        IOrderedEnumerable<T>? ordered = null;
        foreach (var sort in sorts)
        {
            var property = typeof(T).GetProperty(sort.Field) ?? throw new InvalidOperationException($"{typeof(T).Name} has no property {sort.Field}.");
            object? Key(T row)
            {
                return property.GetValue(row);
            }

            ordered = ordered is null
                ? (sort.Ascending ? rows.OrderBy(Key, Comparer<object?>.Default) : rows.OrderByDescending(Key, Comparer<object?>.Default))
                : (sort.Ascending ? ordered.ThenBy(Key, Comparer<object?>.Default) : ordered.ThenByDescending(Key, Comparer<object?>.Default));
        }

        return ordered!;
    }

    private sealed record QueryPlan(List<Filter> Sql, List<Filter> Residual, string? FullText, bool SortsInSql);

    private sealed record SqlQuery(string Sql, Dictionary<string, object?> Parameters);
}
