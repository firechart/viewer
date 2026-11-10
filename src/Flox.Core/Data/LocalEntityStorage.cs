using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Data;

// An IStorage of the documents kept on the device (ILocalDocumentStore): all of them are loaded once into memory and queried there, every change is
// written through. Made for thousands of rows, not for millions. The device has one user (one account of the operating system is one user of the
// application): no owners, no rights of rows, no permissions; a sign-in changes nothing. Changes of another tab are seen after a reload
public class LocalEntityStorage<TKey, TEntity> : IStorage<TKey, TEntity>, IDisposable where TKey : notnull where TEntity : class, IEntity<TKey>
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private static readonly TextSearch s_search = new();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger _logger;
    private readonly ILocalDocumentStore _store;
    private readonly IValidator<TEntity>? _validator;
    private Dictionary<TKey, TEntity>? _rows;

    public LocalEntityStorage(ILocalDocumentStore store, ILogger<LocalEntityStorage<TKey, TEntity>> logger, IValidator<TEntity>? validator = null, string? collection = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = validator;
        Collection = string.IsNullOrWhiteSpace(collection) ? typeof(TEntity).Name : collection;
    }

    public string Collection { get; }

    public async Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var match = predicate?.Compile();
        return await ReadAsync(rows => match is null ? rows.Count : rows.Values.LongCount(match), cancellationToken);
    }

    public async Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
    {
        return await ReadAsync(rows => rows.ContainsKey(key), cancellationToken);
    }

    public async Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default)
    {
        return await ReadAsync(rows => rows.TryGetValue(key, out var row) ? Clone(row) : null, cancellationToken);
    }

    public async Task<PagedResult<TEntity>> FindAsync(PagedRequest<TEntity> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await ReadAsync(rows => Find(rows.Values, request), cancellationToken);
    }

    public async Task<IReadOnlyList<EntityCapability<TKey>>> GetCapabilitiesAsync(IReadOnlyCollection<TKey> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return await ReadAsync(rows => (IReadOnlyList<EntityCapability<TKey>>)[.. keys.Distinct().Select(key => rows.ContainsKey(key) ? new EntityCapability<TKey>(key, true, true, true) : new EntityCapability<TKey>(key, false, false, false))], cancellationToken);
    }

    public Task<TEntity> SetAccessAsync(TKey key, AccessGrant readAccess, AccessGrant writeAccess, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} keeps the documents of one user: they have no access to change.");
    }

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await ValidateAsync(entity, cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rows = await RowsAsync(cancellationToken);
            if (rows.ContainsKey(entity.Uid))
            {
                throw new DuplicateException(entity.Uid);
            }

            entity.CreatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = null;
            entity.Revision = 1;

            var stored = Clone(entity);
            await _store.PutAsync(Collection, [Document(stored)], cancellationToken);
            rows[stored.Uid] = stored;
            return entity;
        }
        finally
        {
            _gate.Release();
        }
    }

    // The revision of the update must be the stored one, as on the server: a copy that was read before another change is refused
    public async Task<TEntity> UpdateAsync(TKey key, TEntity value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        KeyUtil.EnsureSameKey(key, value.Uid);
        await ValidateAsync(value, cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rows = await RowsAsync(cancellationToken);
            var persisted = rows.GetValueOrDefault(key) ?? throw new NotFoundException(key);
            if (value.Revision != persisted.Revision)
            {
                throw new ConcurrentUpdateException(key);
            }

            value.CopyImmutableFieldsFrom(persisted);
            value.UpdatedAt = DateTimeOffset.UtcNow;
            value.Revision = persisted.Revision + 1;

            var stored = Clone(value);
            await _store.PutAsync(Collection, [Document(stored)], cancellationToken);
            rows[key] = stored;
            return value;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rows = await RowsAsync(cancellationToken);
            if (!rows.ContainsKey(key))
            {
                throw new NotFoundException(key);
            }

            await _store.DeleteAsync(Collection, [KeyText(key)], cancellationToken);
            rows.Remove(key);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<long> DeleteManyAsync(Expression<Func<TEntity, bool>> expression, int limit = Constants.MAX_BATCH_DELETE, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var rows = await RowsAsync(cancellationToken);
            var match = expression.Compile();
            var keys = rows.Values.Where(match).Select(row => row.Uid).ToList();
            if (keys.Count > limit)
            {
                throw new BatchLimitExceededException(keys.Count, limit);
            }

            if (keys.Count > 0)
            {
                await _store.DeleteAsync(Collection, [.. keys.Select(KeyText)], cancellationToken);
                foreach (var key in keys)
                {
                    rows.Remove(key);
                }
            }

            return keys.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gate.Dispose();
        }
    }

    // LINQ to objects with delegates compiled once per request: an IQueryable over the rows would compile the tree at every execution, which the
    // interpreter of WebAssembly pays dearly for
    private static PagedResult<TEntity> Find(IEnumerable<TEntity> rows, PagedRequest<TEntity> request)
    {
        var filter = PagedRequestTranslator.BuildFilterExpression<TEntity>(request.Filters);
        if (filter is not null)
        {
            rows = rows.Where(filter.Compile());
        }

        // A search without sorts of its own keeps the order of the relevance, as the full-text search of the server
        var searched = s_search.Apply(rows, request.FullTextSearchString);
        var ordered = request.Sorts.Count > 0 ? Sorted(searched ?? rows, request.Sorts) : searched ?? rows.OrderBy(row => row.Uid);
        var matches = ordered as IReadOnlyCollection<TEntity> ?? [.. ordered];

        var result = new PagedResult<TEntity>();
        if (request.ReturnTotal)
        {
            result.Total = matches.Count;
        }

        IEnumerable<TEntity> page = matches;
        if (request.Skip > 0)
        {
            page = page.Skip(request.Skip);
        }

        if (request.Top > 0)
        {
            page = page.Take(request.Top);
        }

        result.Items.AddRange(page.Select(Clone));
        return result;
    }

    // The fields of the sorts are properties of the entity (a dotted path for a nested one), as for the other storages
    private static IOrderedEnumerable<TEntity> Sorted(IEnumerable<TEntity> rows, IReadOnlyList<Sort> sorts)
    {
        IOrderedEnumerable<TEntity>? ordered = null;
        foreach (var sort in sorts)
        {
            var key = KeyOf(sort.Field);
            ordered = ordered is null
                ? (sort.Ascending ? rows.OrderBy(key, Comparer<object?>.Default) : rows.OrderByDescending(key, Comparer<object?>.Default))
                : (sort.Ascending ? ordered.ThenBy(key, Comparer<object?>.Default) : ordered.ThenByDescending(key, Comparer<object?>.Default));
        }

        return ordered!;
    }

    private static Func<TEntity, object?> KeyOf(string field)
    {
        var row = Expression.Parameter(typeof(TEntity), "row");
        Expression key = row;
        foreach (var part in field.Split('.'))
        {
            key = Expression.PropertyOrField(key, part);
        }

        return Expression.Lambda<Func<TEntity, object?>>(Expression.Convert(key, typeof(object)), row).Compile();
    }

    private async Task<TResult> ReadAsync<TResult>(Func<Dictionary<TKey, TEntity>, TResult> read, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return read(await RowsAsync(cancellationToken));
        }
        finally
        {
            _gate.Release();
        }
    }

    // Under the gate. A document that cannot be read (another version of the class, a broken record) is left out and stays in the store
    private async Task<Dictionary<TKey, TEntity>> RowsAsync(CancellationToken cancellationToken)
    {
        if (_rows is not null)
        {
            return _rows;
        }

        var documents = await _store.LoadAsync(Collection, cancellationToken);
        var rows = new Dictionary<TKey, TEntity>(documents.Count);
        foreach (var document in documents)
        {
            TEntity? row = null;
            try
            {
                row = JsonSerializer.Deserialize<TEntity>(document.Json, s_json);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "A local document is not readable and is left out: Collection={Collection}, Key={Key}", Collection, document.Key);
            }

            if (row is not null)
            {
                rows[row.Uid] = row;
            }
        }

        _rows = rows;
        return rows;
    }

    private async Task ValidateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        if (_validator is not null)
        {
            await _validator.ValidateAndThrowAsync(entity, cancellationToken);
        }
    }

    // The callers get copies: a change of a returned row reaches the store only through UpdateAsync
    private static TEntity Clone(TEntity row)
    {
        return JsonSerializer.Deserialize<TEntity>(JsonSerializer.Serialize(row, s_json), s_json)!;
    }

    private static LocalDocument Document(TEntity row)
    {
        return new LocalDocument(KeyText(row.Uid), JsonSerializer.Serialize(row, s_json));
    }

    private static string KeyText(TKey key)
    {
        return Convert.ToString(key, CultureInfo.InvariantCulture)!;
    }

    // The search of PagedRequest.FullTextSearchString over the string properties marked [Index(Like = true)] or [Index(FullText = ...)], case-insensitive:
    // every word of the text is in one of them. The order: the whole text equals a Like property, starts it, is in it, then the rest; the shorter value first
    private sealed class TextSearch
    {
        private readonly Func<TEntity, string?>[] _like;
        private readonly Func<TEntity, string?>[] _all;

        public TextSearch()
        {
            var marked = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(property => property.PropertyType == typeof(string) && property.CanRead)
                                        .Select(property => new MarkedProperty(property, property.GetCustomAttribute<IndexAttribute>()))
                                        .Where(row => row.Index is not null && (row.Index.Like || row.Index.FullText != FullTextWeight.None))
                                        .ToList();
            _like = [.. marked.Where(row => row.Index!.Like).Select(row => Getter(row.Property))];
            _all = [.. marked.Select(row => Getter(row.Property))];
        }

        // Null: no search (no text, or an entity with nothing to search), the query stays as it is
        public IEnumerable<TEntity>? Apply(IEnumerable<TEntity> rows, string? text)
        {
            var whole = text?.Trim();
            if (string.IsNullOrEmpty(whole) || _all.Length == 0)
            {
                return null;
            }

            var words = whole.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return rows.Where(row => words.All(word => _all.Any(read => Contains(read(row), word))))
                        .Select(row => new Ranked(row, Rank(row, whole)))
                        .OrderBy(row => row.Rank.Tier)
                        .ThenBy(row => row.Rank.Length)
                        .Select(row => row.Row)
                        .ToList();
        }

        private Relevance Rank(TEntity row, string whole)
        {
            var best = new Relevance(3, int.MaxValue);
            foreach (var read in _like)
            {
                var value = read(row);
                if (value is null)
                {
                    continue;
                }

                var tier = value.Equals(whole, StringComparison.OrdinalIgnoreCase) ? 0 : value.StartsWith(whole, StringComparison.OrdinalIgnoreCase) ? 1 : Contains(value, whole) ? 2 : 3;
                var rank = new Relevance(tier, value.Length);
                if (rank.Tier < best.Tier || (rank.Tier == best.Tier && rank.Length < best.Length))
                {
                    best = rank;
                }
            }

            return best;
        }

        private static bool Contains(string? value, string text)
        {
            return value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase);
        }

        private static Func<TEntity, string?> Getter(PropertyInfo property)
        {
            var row = Expression.Parameter(typeof(TEntity), "row");
            return Expression.Lambda<Func<TEntity, string?>>(Expression.Property(row, property), row).Compile();
        }

        private sealed record MarkedProperty(PropertyInfo Property, IndexAttribute? Index);

        private readonly record struct Relevance(int Tier, int Length);

        private sealed record Ranked(TEntity Row, Relevance Rank);
    }
}

public class LocalGuidEntityStorage<TEntity>
(
    ILocalDocumentStore store,
    ILogger<LocalEntityStorage<Guid, TEntity>> logger,
    IValidator<TEntity>? validator = null,
    string? collection = null
) : LocalEntityStorage<Guid, TEntity>(store, logger, validator, collection), IGuidEntityStorage<TEntity> where TEntity : class, IGuidEntity;

public class LocalStringEntityStorage<TEntity>
(
    ILocalDocumentStore store,
    ILogger<LocalEntityStorage<string, TEntity>> logger,
    IValidator<TEntity>? validator = null,
    string? collection = null
) : LocalEntityStorage<string, TEntity>(store, logger, validator, collection), IStringEntityStorage<TEntity> where TEntity : class, IStringEntity;
