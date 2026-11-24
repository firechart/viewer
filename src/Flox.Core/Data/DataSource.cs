using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Data;

/// <summary>A data source of entities keyed by <see cref="Guid"/>.</summary>
public interface IGuidDataSource<TEntity> : IDataSource<Guid, TEntity> // where TEntity : IGuidEntity
{
}

/// <summary>
/// The facade for UI and services over a storage: paging, filters, sorts and the entity operations, with the state of the last load.
/// </summary>
public interface IDataSource<TKey, TEntity> : IDataSourceEvents where TKey : notnull
{
    /// <summary>The zero-based number of the current page.</summary>
    int CurrentPage { get; }
    /// <summary>The message of the last failed operation, or null when it succeeded.</summary>
    string? ErrorMessage { get; }
    /// <summary>The names of the fields to load; empty means all of them.</summary>
    List<string> Fields { get; }
    /// <summary>Free text to search for, if the storage supports it.</summary>
    string? FullTextSearchString { get; set; }
    /// <summary>Tells whether a page follows the current one.</summary>
    bool HasNextPage { get; }
    /// <summary>Tells whether a page precedes the current one.</summary>
    bool HasPreviousPage { get; }
    /// <summary>The entities of the current page.</summary>
    List<TEntity> Items { get; }
    /// <summary>The exception of the last failed operation, or null when it succeeded.</summary>
    Exception? LastException { get; }
    /// <summary>How the pages are addressed: by number or by the links of the previous answer.</summary>
    PaginationMode Mode { get; set; }
    /// <summary>The number of entities on a page.</summary>
    int PageSize { get; set; }
    /// <summary>The request that the next load sends.</summary>
    PagedRequest<TEntity> Request { get; }
    /// <summary>The answer to the last load.</summary>
    PagedResult<TEntity> Response { get; }
    /// <summary>The storage that the source works over.</summary>
    IStorage<TKey, TEntity> Storage { get; }
    /// <summary>The total number of entities that match the filters, as the last load returned it.</summary>
    long TotalItems { get; }
    /// <summary>The number of pages that the total gives at the current page size.</summary>
    int TotalPages { get; }

    /// <summary>Counts the entities that match the current filters.</summary>
    Task<long> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes the entity and reloads the current page.</summary>
    Task DeleteAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the entities of the expression that the caller may delete, up to the limit; see <see cref="IStorage{TKey,
    /// TEntity}.DeleteManyAsync"/> for the limit. Returns the number deleted.
    /// </summary>
    Task<long> DeleteManyAsync(Expression<Func<TEntity, bool>> expression, int limit = Constants.MAX_BATCH_DELETE, bool refresh = true, CancellationToken cancellationToken = default);

    /// <summary>Tells whether the entity exists for the caller.</summary>
    Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>Loads the first page.</summary>
    Task FirstPageAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the entity with the key, or null when there is none for the caller.</summary>
    Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>Returns the key of the entity.</summary>
    TKey GetKey(TEntity entity);

    /// <summary>Loads the page with the given zero-based number, if it exists.</summary>
    Task GoToPageAsync(int page, CancellationToken cancellationToken = default);

    /// <summary>Inserts the entity, optionally reloading the current page, and returns the stored one.</summary>
    Task<TEntity> InsertAsync(TEntity entity, bool refresh = true, CancellationToken cancellationToken = default);

    /// <summary>Loads the last page.</summary>
    Task LastPageAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads the current page with the current request.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads the next page, if there is one.</summary>
    Task NextPageAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads the previous page, if there is one.</summary>
    Task PreviousPageAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears the request and the answer.</summary>
    IDataSource<TKey, TEntity> Reset();

    /// <summary>Clears fields, filters, sorts and extra parameters, and keeps the paging.</summary>
    IDataSource<TKey, TEntity> ResetFiltersAndSorts();

    /// <summary>Sets whether a load also returns the total.</summary>
    IDataSource<TKey, TEntity> SetCount(bool count);

    /// <summary>Sets the names of the fields to load.</summary>
    IDataSource<TKey, TEntity> SetFields(List<string> fields);

    /// <summary>Sets the fields to load from the members that the selector picks.</summary>
    IDataSource<TKey, TEntity> SetFields<TResult>(Expression<Func<TEntity, TResult>> selector);

    /// <summary>Sets the zero-based page number of the next load.</summary>
    IDataSource<TKey, TEntity> SetPage(int page);

    /// <summary>Sets the number of entities on a page.</summary>
    IDataSource<TKey, TEntity> SetPageSize(int pageSize);

    /// <summary>Sets the number of entities to skip.</summary>
    IDataSource<TKey, TEntity> SetSkip(int skip);

    /// <summary>Replaces the access list of the entity and returns the stored one, see <see cref="IStorage{TKey, TEntity}.SetAccessAsync"/>.</summary>
    Task<IReadOnlyList<AclEntry>> SetAccessAsync(TKey key, IReadOnlyCollection<AclEntry> acl, CancellationToken cancellationToken = default);

    /// <summary>Replaces the entity under the key and reloads the current page.</summary>
    Task<TEntity> UpdateAsync(TKey key, TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Inserts the entity, or replaces the stored one with the same key.</summary>
    Task<TEntity> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>The filters of the request.</summary>
    FilterCollection<TEntity> Filters { get; }

    /// <summary>The sorts of the request.</summary>
    SortCollection<TEntity> Sorts { get; }
}

/// <summary>The data source of a <see cref="IGuidEntity"/> over its storage.</summary>
public sealed class GuidDataSource<TEntity> : DataSource<Guid, TEntity>, IGuidDataSource<TEntity> where TEntity : IGuidEntity
{
    /// <summary>Creates the source over the storage.</summary>
    public GuidDataSource(IGuidEntityStorage<TEntity> storage, ILogger<DataSource<Guid, TEntity>> logger) : base(storage, logger)
    {
    }
}

/// <summary>The default data source: a request, the last answer, paging and the busy state over an <see cref="IStorage{TKey, TEntity}"/>.</summary>
public class DataSource<TKey, TEntity> : IDataSource<TKey, TEntity> where TKey : notnull
{
    private readonly ILogger<DataSource<TKey, TEntity>> _logger;
    private int _busyCount;

    /// <summary>Creates the source over the storage.</summary>
    public DataSource(IStorage<TKey, TEntity> storage, ILogger<DataSource<TKey, TEntity>> logger)
    {
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler? BusyChanged;

    /// <inheritdoc />
    public int CurrentPage
    {
        get
        {
            return Request.Page;
        }
    }

    /// <inheritdoc />
    public string? ErrorMessage { get; private set; }

    /// <inheritdoc />
    public List<string> Fields
    {
        get
        {
            return Request.Fields;
        }
    }

    /// <inheritdoc />
    public string? FullTextSearchString
    {
        get
        {
            return Request.FullTextSearchString;
        }

        set
        {
            Request.FullTextSearchString = value;
        }
    }

    /// <inheritdoc />
    public bool HasNextPage
    {
        get
        {
            if (Mode == PaginationMode.PageBased)
            {
                return CurrentPage < TotalPages - 1;
            }

            return !string.IsNullOrEmpty(Response.NextLink);
        }
    }

    /// <inheritdoc />
    public bool HasPreviousPage
    {
        get
        {
            if (Mode == PaginationMode.PageBased)
            {
                return CurrentPage > 0;
            }

            return !string.IsNullOrEmpty(Response.PrevLink);
        }
    }

    /// <inheritdoc />
    public bool IsBusy
    {
        get
        {
            return _busyCount > 0;
        }
    }

    /// <inheritdoc />
    public List<TEntity> Items
    {
        get
        {
            return Response.Items;
        }
    }

    /// <inheritdoc />
    public Exception? LastException { get; private set; }

    /// <inheritdoc />
    public PaginationMode Mode { get; set; }

    /// <inheritdoc />
    public int PageSize
    {
        get
        {
            return Request.PageSize;
        }

        set
        {
            Request.PageSize = value;
        }
    }

    /// <inheritdoc />
    public PagedRequest<TEntity> Request { get; private set; } = new PagedRequest<TEntity>();

    /// <inheritdoc />
    public PagedResult<TEntity> Response { get; private set; } = new PagedResult<TEntity>();

    /// <inheritdoc />
    public IStorage<TKey, TEntity> Storage { get; }

    /// <inheritdoc />
    public long TotalItems
    {
        get
        {
            return Response.Total ?? 0;
        }
    }

    /// <inheritdoc />
    public int TotalPages
    {
        get
        {
            return PageSize > 0 && TotalItems > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
        }
    }

    /// <inheritdoc />
    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await Storage.CountAsync(Request.Filters, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async (cancellationToken) =>
                           {
                               await Storage.DeleteAsync(key, cancellationToken);
                               await LoadAsync(cancellationToken);
                           },
                           cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> DeleteManyAsync(Expression<Func<TEntity, bool>> expression, int limit = Constants.MAX_BATCH_DELETE, bool refresh = true, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async cancellationToken =>
                                  {
                                      var deleted = await Storage.DeleteManyAsync(expression, limit, cancellationToken);

                                      if (refresh)
                                      {
                                          await LoadAsync(cancellationToken);
                                      }

                                      return deleted;
                                  },
                                  cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
    {
        return await Storage.ExistsAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task FirstPageAsync(CancellationToken cancellationToken = default)
    {
        await NavigateAsync(() =>
                            {
                                Request.Page = 0;
                                if (Mode != PaginationMode.PageBased)
                                {
                                    Request.Link = Response.FirstLink;
                                }
                            },
                            cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default)
    {
        return await Storage.GetByIdAsync(key, cancellationToken);
    }

    /// <summary>Returns the key of the entity; an entity that is not an <see cref="IEntity{TKey}"/> (a third-party model) overrides it.</summary>
    public virtual TKey GetKey(TEntity entity)
    {
        return entity is IEntity<TKey> keyed ? keyed.Uid : throw new NotSupportedException($"{typeof(TEntity).Name} has no key: override GetKey");
    }

    /// <inheritdoc />
    public async Task GoToPageAsync(int page, CancellationToken cancellationToken = default)
    {
        if (page >= 0 && page < TotalPages)
        {
            await NavigateAsync(() => Request.Page = page, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<TEntity> InsertAsync(TEntity entity, bool refresh = true, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async cancellationToken =>
                                  {
                                      var result = await Storage.InsertAsync(entity, cancellationToken);

                                      if (refresh)
                                      {
                                          await LoadAsync(cancellationToken);
                                      }

                                      return result;
                                  },
                                  cancellationToken);
    }

    /// <inheritdoc />
    public async Task LastPageAsync(CancellationToken cancellationToken = default)
    {
        if (Mode == PaginationMode.PageBased)
        {
            if (TotalPages > 0)
            {
                await NavigateAsync(() => Request.Page = TotalPages - 1, cancellationToken);
            }
        }
        else if (Response.LastLink != null)
        {
            await NavigateAsync(() => Request.Link = Response.LastLink, cancellationToken);
        }
    }

    /// <inheritdoc />
    public virtual async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            EnterBusy();
            ErrorMessage = null;
            LastException = null;
            Response = await Storage.FindAsync(Request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("LoadAsync was cancelled for {Type}", typeof(TEntity).Name);
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            LastException = ex;
            throw;
        }
        finally
        {
            ExitBusy();
        }
    }

    /// <inheritdoc />
    public async Task NextPageAsync(CancellationToken cancellationToken = default)
    {
        if (HasNextPage)
        {
            await NavigateAsync(() =>
                                {
                                    Request.Page++;
                                    if (Mode != PaginationMode.PageBased)
                                    {
                                        Request.Link = Response.NextLink;
                                    }
                                },
                                cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task PreviousPageAsync(CancellationToken cancellationToken = default)
    {
        if (HasPreviousPage)
        {
            await NavigateAsync(() =>
                                {
                                    Request.Page--;
                                    if (Mode != PaginationMode.PageBased)
                                    {
                                        Request.Link = Response.PrevLink;
                                    }
                                },
                                cancellationToken);
        }
    }

    /// <inheritdoc />
    public virtual IDataSource<TKey, TEntity> Reset()
    {
        Request.Reset();
        Response = new PagedResult<TEntity>();
        ErrorMessage = null;
        LastException = null;
        return this;
    }

    /// <inheritdoc />
    public virtual IDataSource<TKey, TEntity> ResetFiltersAndSorts()
    {
        Fields.Clear();
        Request.FullTextSearchString = null;
        Request.Filters.Clear();
        Request.Sorts.Clear();
        Request.CustomQueryParameters.Clear();
        ErrorMessage = null;
        LastException = null;
        return this;
    }

    /// <inheritdoc />
    public IDataSource<TKey, TEntity> SetCount(bool count)
    {
        Request.ReturnTotal = count;
        return this;
    }

    /// <inheritdoc />
    public IDataSource<TKey, TEntity> SetFields(List<string> fields)
    {
        Request.Fields.Clear();
        Request.Fields.AddRange(fields);
        return this;
    }

    /// <inheritdoc />
    public IDataSource<TKey, TEntity> SetFields<TResult>(Expression<Func<TEntity, TResult>> selector)
    {
        var fieldNames = ExtractFieldNamesFromSelector(selector);
        Request.Fields.Clear();
        Request.Fields.AddRange(fieldNames);
        return this;
    }

    /// <inheritdoc />
    public IDataSource<TKey, TEntity> SetPage(int page)
    {
        Request.Page = page;
        return this;
    }

    /// <inheritdoc />
    public IDataSource<TKey, TEntity> SetPageSize(int pageSize)
    {
        Request.PageSize = pageSize;
        return this;
    }

    /// <inheritdoc />
    public IDataSource<TKey, TEntity> SetSkip(int skip)
    {
        Request.Skip = skip;
        return this;
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<AclEntry>> SetAccessAsync(TKey key, IReadOnlyCollection<AclEntry> acl, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async ct =>
                                  {
                                      var result = await Storage.SetAccessAsync(key, acl, ct);
                                      await LoadAsync(ct);
                                      return result;
                                  },
                                  cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> UpdateAsync(TKey key, TEntity entity, CancellationToken cancellationToken = default)
    {
        KeyUtil.EnsureSameKey(key, GetKey(entity));

        return await ExecuteAsync(async ct =>
                                  {
                                      // The storage finds the document under the rights of the caller (NotFoundException); no read of ours first
                                      var result = await Storage.UpdateAsync(key, entity, ct);
                                      await LoadAsync(ct);
                                      return result;
                                  },
                                  cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async ct =>
                                  {
                                      var key = GetKey(entity);
                                      var current = await Storage.GetByIdAsync(key, ct);
                                      if (current is null)
                                      {
                                          return await InsertAsync(entity, cancellationToken: ct);
                                      }

                                      // An upsert replaces what is there: it takes the revision of what it replaces
                                      if (entity is IEntity incoming && current is IEntity present)
                                      {
                                          incoming.Revision = present.Revision;
                                      }

                                      return await UpdateAsync(key, entity, ct);
                                  },
                                  cancellationToken);
    }

    /// <inheritdoc />
    public FilterCollection<TEntity> Filters
    {
        get
        {
            return Request.Filters;
        }
    }

    /// <inheritdoc />
    public SortCollection<TEntity> Sorts
    {
        get
        {
            return Request.Sorts;
        }
    }

    private static List<string> ExtractFieldNamesFromSelector<TResult>(Expression<Func<TEntity, TResult>> selector)
    {
        var fieldNames = new List<string>();

        if (selector.Body is NewExpression newExpression)
        {
            foreach (var argument in newExpression.Arguments)
            {
                if (argument is MemberExpression memberExpression)
                {
                    fieldNames.Add(memberExpression.Member.Name);
                }
                else if (argument is UnaryExpression { Operand: MemberExpression unaryMember })
                {
                    fieldNames.Add(unaryMember.Member.Name);
                }
            }
        }
        else if (selector.Body is MemberInitExpression memberInitExpression)
        {
            foreach (var binding in memberInitExpression.Bindings)
            {
                if (binding is MemberAssignment assignment && assignment.Expression is MemberExpression memberExpr)
                {
                    fieldNames.Add(memberExpr.Member.Name);
                }
            }
        }
        else if (selector.Body is MemberExpression singleMember)
        {
            fieldNames.Add(singleMember.Member.Name);
        }

        if (fieldNames.Count == 0)
        {
            throw new ArgumentException($"Expression '{selector}' does not contain valid property selections. " + "Use anonymous type initializer: x => new {{ x.Property1, x.Property2 }}", nameof(selector));
        }

        return fieldNames;
    }

    private static bool IsNull(TEntity? value)
    {
        var result = EqualityComparer<TEntity>.Default.Equals(value, default);
        return result;
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await ExecuteAsync<object?>(async ct =>
                                    {
                                        await action(ct);
                                        return null;
                                    },
                                    cancellationToken);
    }

    private async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
    {
        try
        {
            EnterBusy();
            ErrorMessage = null;
            LastException = null;
            return await action(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            LastException = ex;
            throw;
        }
        finally
        {
            ExitBusy();
        }
    }

    private void EnterBusy()
    {
        if (++_busyCount == 1)
        {
            BusyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ExitBusy()
    {
        if (_busyCount > 0 && --_busyCount == 0)
        {
            BusyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Restores the page and the link when the load fails or is cancelled: Response still holds the previous page
    private async Task NavigateAsync(Action move, CancellationToken cancellationToken)
    {
        var page = Request.Page;
        var link = Request.Link;
        move();
        try
        {
            await LoadAsync(cancellationToken);
        }
        catch
        {
            Request.Page = page;
            Request.Link = link;
            throw;
        }
    }
}
