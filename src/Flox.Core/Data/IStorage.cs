using System.Linq.Expressions;

namespace Flox.Core.Data;

public interface IStorage<TKey, TEntity> where TKey : notnull
{
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <remarks>
    ///     Applies read authorization, like GetByIdAsync: false when the entity does not exist or the caller may not read it
    /// </remarks>
    Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default);

    Task DeleteAsync(TKey key, CancellationToken cancellationToken = default);

    /// <remarks>
    ///     Deletes nothing and throws BatchLimitExceededException when more than <paramref name="limit" /> entities match.
    ///     Only administrators may ask for a limit above Constants.MAX_BATCH_DELETE
    /// </remarks>
    Task<long> DeleteManyAsync(Expression<Func<TEntity, bool>> expression, int limit = Constants.MAX_BATCH_DELETE, CancellationToken cancellationToken = default);

    // The one way to read a list: the filters, sorts, window, search and bookmarks of the request, under the rights of the caller
    Task<PagedResult<TEntity>> FindAsync(PagedRequest<TEntity> request, CancellationToken cancellationToken = default);

    /// <remarks>
    ///     Applies read authorization: null when the entity does not exist or the caller may not read it
    /// </remarks>
    Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default);

    Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <remarks>
    ///     What the caller may do with each document: read, change, delete. A document that does not exist or that the caller may not read
    ///     is answered with false in all three. The answer is in the order of the keys
    /// </remarks>
    Task<IReadOnlyList<EntityCapability<TKey>>> GetCapabilitiesAsync(IReadOnlyCollection<TKey> keys, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} does not tell what the caller may do with a document.");
    }

    /// <remarks>
    ///     The only way to change who may read and write a document: both fields are immutable for an ordinary update.
    ///     Administrators only. A replicated document that everyone could read cannot be made narrower (clients hold copies
    ///     and a change of rights is not told to them): it is deleted instead
    /// </remarks>
    Task<TEntity> SetAccessAsync(TKey key, AccessGrant readAccess, AccessGrant writeAccess, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} does not change the access of a document.");
    }

    Task<TEntity> UpdateAsync(TKey key, TEntity value, CancellationToken cancellationToken = default);
}

public interface IGuidEntityStorage<TEntity> : IStorage<Guid, TEntity> where TEntity : IGuidEntity
{
}

public interface IStringEntityStorage<TEntity> : IStorage<string, TEntity> where TEntity : IStringEntity
{
}
