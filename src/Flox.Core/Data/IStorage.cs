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
    ///     What the caller may do with each document: the operations of the entity that the permissions of the table and the access list of
    ///     the row allow. A document that does not exist or that the caller may not read is answered with no operations. The answer is in
    ///     the order of the keys
    /// </remarks>
    Task<IReadOnlyList<EntityCapability<TKey>>> GetCapabilitiesAsync(IReadOnlyCollection<TKey> keys, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} does not tell what the caller may do with a document.");
    }

    /// <summary>Stores a new entity with an access list of the writer's instead of the default one; it needs the permission ManageAccess of the table.</summary>
    Task<TEntity> InsertAsync(TEntity entity, IReadOnlyCollection<AclEntry> acl, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} keeps no access lists.");
    }

    /// <remarks>
    ///     The access list of a document; it needs the operation ManageAccess on the document (the permission of the table and an entry of
    ///     the list). A document the caller may not read is not found
    /// </remarks>
    Task<IReadOnlyList<AclEntry>> GetAccessAsync(TKey key, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} keeps no access lists.");
    }

    /// <remarks>
    ///     The only way to change who may do what with a document: replaces its access list and returns the stored one. It needs the
    ///     operation ManageAccess on the document; every operation of the list must be one of the entity
    /// </remarks>
    Task<IReadOnlyList<AclEntry>> SetAccessAsync(TKey key, IReadOnlyCollection<AclEntry> acl, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} keeps no access lists.");
    }

    /// <summary>Replaces the entity under the key and returns the stored one.</summary>
    Task<TEntity> UpdateAsync(TKey key, TEntity value, CancellationToken cancellationToken = default);
}

/// <summary>A storage of <see cref="IGuidEntity"/> documents.</summary>
public interface IGuidEntityStorage<TEntity> : IStorage<Guid, TEntity> where TEntity : IGuidEntity
{
}
