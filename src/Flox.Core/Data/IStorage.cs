using System.Linq.Expressions;

namespace Flox.Core.Data;

/// <summary>The persistence abstraction: reads and writes of documents under the rights of the caller.</summary>
public interface IStorage<TKey, TEntity> where TKey : notnull
{
    /// <summary>Counts the entities that match the predicate, or all of them when it is null.</summary>
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the entities that match the filters of a request; a storage that reads them better than an expression (SQL) answers it itself.
    /// </summary>
    Task<long> CountAsync(FilterCollection<TEntity> filters, CancellationToken cancellationToken = default)
    {
        return CountAsync(PagedRequestTranslator.BuildFilterExpression<TEntity>(filters), cancellationToken);
    }

    /// <remarks>
    ///     Applies read authorization, like GetByIdAsync: false when the entity does not exist or the caller may not read it
    /// </remarks>
    Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>Deletes the entity with the key.</summary>
    Task DeleteAsync(TKey key, CancellationToken cancellationToken = default);

    /// <remarks>
    ///     Deletes nothing and throws BatchLimitExceededException when more than <paramref name="limit" /> entities match.
    ///     Only administrators may ask for a limit above Constants.MAX_BATCH_DELETE
    /// </remarks>
    Task<long> DeleteManyAsync(Expression<Func<TEntity, bool>> expression, int limit = Constants.MAX_BATCH_DELETE, CancellationToken cancellationToken = default);

    /// <summary>The one way to read a list: the filters, sorts, window, search and bookmarks of the request, under the rights of the caller.</summary>
    Task<PagedResult<TEntity>> FindAsync(PagedRequest<TEntity> request, CancellationToken cancellationToken = default);

    /// <remarks>
    ///     Applies read authorization: null when the entity does not exist or the caller may not read it
    /// </remarks>
    Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>Stores a new entity and returns the stored one.</summary>
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
    ///     Administrators only.
    /// </remarks>
    Task<TEntity> SetAccessAsync(TKey key, AccessGrant readAccess, AccessGrant writeAccess, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} does not change the access of a document.");
    }

    /// <summary>Replaces the entity under the key and returns the stored one.</summary>
    Task<TEntity> UpdateAsync(TKey key, TEntity value, CancellationToken cancellationToken = default);
}

/// <summary>A storage of <see cref="IGuidEntity"/> documents.</summary>
public interface IGuidEntityStorage<TEntity> : IStorage<Guid, TEntity> where TEntity : IGuidEntity
{
}

/// <summary>A storage of <see cref="IStringEntity"/> documents.</summary>
public interface IStringEntityStorage<TEntity> : IStorage<string, TEntity> where TEntity : IStringEntity
{
}
