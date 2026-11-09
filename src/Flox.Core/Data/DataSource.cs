using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Data;

public interface IGuidDataSource<TEntity> : IDataSource<Guid, TEntity> // where TEntity : IGuidEntity
{
}

public interface IDataSource<TKey, TEntity> : IDataSourceEvents where TKey : notnull
{
    int CurrentPage { get; }
    string? ErrorMessage { get; }
    List<string> Fields { get; }
    string? FullTextSearchString { get; set; }
    bool HasNextPage { get; }
    bool HasPreviousPage { get; }
    List<TEntity> Items { get; }
    Exception? LastException { get; }
    PaginationMode Mode { get; set; }
    int PageSize { get; set; }
    PagedRequest<TEntity> Request { get; }
    PagedResult<TEntity> Response { get; }
    IStorage<TKey, TEntity> Storage { get; }
    long TotalItems { get; }
    int TotalPages { get; }

    Task<long> CountAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(TKey key, CancellationToken cancellationToken = default);

    // The rows of the expression that the caller may delete; see IStorage.DeleteManyAsync for the limit
    Task<long> DeleteManyAsync(Expression<Func<TEntity, bool>> expression, int limit = Constants.MAX_BATCH_DELETE, bool refresh = true, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default);

    Task FirstPageAsync(CancellationToken cancellationToken = default);

    Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default);

    TKey GetKey(TEntity entity);

    Task GoToPageAsync(int page, CancellationToken cancellationToken = default);

    Task<TEntity> InsertAsync(TEntity entity, bool refresh = true, CancellationToken cancellationToken = default);

    Task LastPageAsync(CancellationToken cancellationToken = default);

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task NextPageAsync(CancellationToken cancellationToken = default);

    Task PreviousPageAsync(CancellationToken cancellationToken = default);

    IDataSource<TKey, TEntity> Reset();

    IDataSource<TKey, TEntity> ResetFiltersAndSorts();

    IDataSource<TKey, TEntity> SetCount(bool count);

    IDataSource<TKey, TEntity> SetFields(List<string> fields);

    IDataSource<TKey, TEntity> SetFields<TResult>(Expression<Func<TEntity, TResult>> selector);

    IDataSource<TKey, TEntity> SetPage(int page);

    IDataSource<TKey, TEntity> SetPageSize(int pageSize);

    IDataSource<TKey, TEntity> SetSkip(int skip);

    // Administrators only, see IStorage.SetAccessAsync
    Task<TEntity> SetAccessAsync(TKey key, AccessGrant readAccess, AccessGrant writeAccess, CancellationToken cancellationToken = default);

    Task<TEntity> UpdateAsync(TKey key, TEntity entity, CancellationToken cancellationToken = default);

    Task<TEntity> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    FilterCollection<TEntity> Filters { get; }

    SortCollection<TEntity> Sorts { get; }
}

public sealed class GuidDataSource<TEntity> : DataSource<Guid, TEntity>, IGuidDataSource<TEntity> where TEntity : IGuidEntity
{
    public GuidDataSource(IGuidEntityStorage<TEntity> storage, ILogger<DataSource<Guid, TEntity>> logger) : base(storage, logger)
    {
    }
}

public class DataSource<TKey, TEntity> : IDataSource<TKey, TEntity> where TKey : notnull
{
    private readonly ILogger<DataSource<TKey, TEntity>> _logger;
    private int _busyCount;

    public DataSource(IStorage<TKey, TEntity> storage, ILogger<DataSource<TKey, TEntity>> logger)
    {
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event EventHandler? BusyChanged;

    public int CurrentPage
    {
        get
        {
            return Request.Page;
        }
    }

    public string? ErrorMessage { get; private set; }

    public List<string> Fields
    {
        get
        {
            return Request.Fields;
        }
    }

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

    public bool IsBusy
    {
        get
        {
            return _busyCount > 0;
        }
    }

    public List<TEntity> Items
    {
        get
        {
            return Response.Items;
        }
    }

    public Exception? LastException { get; private set; }

    public PaginationMode Mode { get; set; }

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

    public PagedRequest<TEntity> Request { get; private set; } = new PagedRequest<TEntity>();

    public PagedResult<TEntity> Response { get; private set; } = new PagedResult<TEntity>();

    public IStorage<TKey, TEntity> Storage { get; }

    public long TotalItems
    {
        get
        {
            return Response.Total ?? 0;
        }
    }

    public int TotalPages
    {
        get
        {
            return PageSize > 0 && TotalItems > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
        }
    }

    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        var q = PagedRequestTranslator.BuildFilterExpression<TEntity>(Request.Filters);
        return await Storage.CountAsync(q, cancellationToken);
    }

    public async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async (cancellationToken) =>
                           {
                               await Storage.DeleteAsync(key, cancellationToken);
                               await LoadAsync(cancellationToken);
                           },
                           cancellationToken);
    }

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

    public async Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
    {
        return await Storage.ExistsAsync(key, cancellationToken);
    }

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

    public virtual async Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default)
    {
        return await Storage.GetByIdAsync(key, cancellationToken);
    }

    // An entity that is not an IEntity<TKey> (a third-party model) overrides it
    public virtual TKey GetKey(TEntity entity)
    {
        return entity is IEntity<TKey> keyed ? keyed.Uid : throw new NotSupportedException($"{typeof(TEntity).Name} has no key: override GetKey");
    }

    public async Task GoToPageAsync(int page, CancellationToken cancellationToken = default)
    {
        if (page >= 0 && page < TotalPages)
        {
            await NavigateAsync(() => Request.Page = page, cancellationToken);
        }
    }

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

    public virtual IDataSource<TKey, TEntity> Reset()
    {
        Request.Reset();
        Response = new PagedResult<TEntity>();
        ErrorMessage = null;
        LastException = null;
        return this;
    }

    // Keeps paging but resets filters
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

    public IDataSource<TKey, TEntity> SetCount(bool count)
    {
        Request.ReturnTotal = count;
        return this;
    }

    public IDataSource<TKey, TEntity> SetFields(List<string> fields)
    {
        Request.Fields.Clear();
        Request.Fields.AddRange(fields);
        return this;
    }

    public IDataSource<TKey, TEntity> SetFields<TResult>(Expression<Func<TEntity, TResult>> selector)
    {
        var fieldNames = ExtractFieldNamesFromSelector(selector);
        Request.Fields.Clear();
        Request.Fields.AddRange(fieldNames);
        return this;
    }

    public IDataSource<TKey, TEntity> SetPage(int page)
    {
        Request.Page = page;
        return this;
    }

    public IDataSource<TKey, TEntity> SetPageSize(int pageSize)
    {
        Request.PageSize = pageSize;
        return this;
    }

    public IDataSource<TKey, TEntity> SetSkip(int skip)
    {
        Request.Skip = skip;
        return this;
    }

    public virtual async Task<TEntity> SetAccessAsync(TKey key, AccessGrant readAccess, AccessGrant writeAccess, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async ct =>
                                  {
                                      var result = await Storage.SetAccessAsync(key, readAccess, writeAccess, ct);
                                      await LoadAsync(ct);
                                      return result;
                                  },
                                  cancellationToken);
    }

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
                                      if (entity is IReplicaState incoming && current is IReplicaState present)
                                      {
                                          incoming.Revision = present.Revision;
                                      }

                                      return await UpdateAsync(key, entity, ct);
                                  },
                                  cancellationToken);
    }

    public FilterCollection<TEntity> Filters
    {
        get
        {
            return Request.Filters;
        }
    }

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
