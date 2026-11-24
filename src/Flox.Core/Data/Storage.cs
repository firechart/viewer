using System.Linq.Expressions;
using Flox.Core.Authorization;
using Flox.Core.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Data;

/// <summary>Checks of keys shared by the storages.</summary>
public static class KeyUtil
{
    /// <summary>Throws when the key of the call differs from the key of the entity.</summary>
    public static void EnsureSameKey<TKey>(TKey key, TKey entityKey) where TKey : notnull
    {
        if (!EqualityComparer<TKey>.Default.Equals(key, entityKey))
        {
            throw new ArgumentException($"The key {key} differs from the key of the entity {entityKey}.", nameof(key));
        }
    }
}

/// <remarks>
///     The interface is relatively simple so the base class can be useful only if a new server
///     database provider (e.g. Postgres, Oracle) is being designed to leverage authorization and audit
/// </remarks>
public abstract class StorageBase<TKey, TEntity> : IStorage<TKey, TEntity> where TKey : notnull where TEntity : IEntity<TKey>
{
    /// <summary>Creates the storage with the services it authorizes and validates with.</summary>
    protected StorageBase(IAuthenticationService authenticationService, ILogger<StorageBase<TKey, TEntity>> logger, IAuthorizationService? authorizationService = null, IValidator<TEntity>? validator = null)
    {
        AuthenticationService = authenticationService;
        Logger = logger;
        AuthorizationService = authorizationService;
        Validator = validator;
    }

    /// <summary>Checks the permissions of the entity table; null skips the permission layer.</summary>
    protected IAuthorizationService? AuthorizationService { get; }
    /// <summary>Tells who the caller is.</summary>
    protected IAuthenticationService AuthenticationService { get; }
    /// <summary>The logger of the storage.</summary>
    protected ILogger Logger { get; }
    /// <summary>Validates entities before they are written, if set.</summary>
    protected IValidator<TEntity>? Validator { get; }
    /// <inheritdoc />
    public abstract Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task DeleteAsync(TKey key, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<long> DeleteManyAsync(Expression<Func<TEntity, bool>> expression, int limit = Constants.MAX_BATCH_DELETE, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<PagedResult<TEntity>> FindAsync(PagedRequest<TEntity> request, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual Task<TEntity> InsertAsync(TEntity entity, IReadOnlyCollection<AclEntry> acl, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} keeps no access lists.");
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<AclEntry>> GetAccessAsync(TKey key, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} keeps no access lists.");
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<AclEntry>> SetAccessAsync(TKey key, IReadOnlyCollection<AclEntry> acl, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} keeps no access lists.");
    }

    /// <inheritdoc />
    public abstract Task<TEntity> UpdateAsync(TKey key, TEntity value, CancellationToken cancellationToken = default);

    /// <summary>Throws when the key of the call differs from the key of the entity.</summary>
    public static void EnsureSameKey(TKey key, TKey entityKey)
    {
        KeyUtil.EnsureSameKey(key, entityKey);
    }

    /// <summary>Returns the key of the entity.</summary>
    protected TKey GetKey(TEntity entity)
    {
        return entity.Uid;
    }

    /// <summary>Checks that the caller may insert.</summary>
    protected virtual async Task AuthorizeInsertAsync(TEntity newValue)
    {
        await EnsureAuthenticatedAsync();
        await RequirePermissionAsync(EntityOperations.Create);
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<EntityCapability<TKey>>> GetCapabilitiesAsync(IReadOnlyCollection<TKey> keys, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} does not tell what the caller may do with a document.");
    }

    /// <summary>
    /// Tells whether the role of the caller may use the table for the operation (the matrix alone, no row): what a list and an insert need.
    /// </summary>
    protected async Task<bool> HasPermissionAsync(string operation)
    {
        if (AuthorizationService is null || !EntityPermissions.IsChecked(typeof(TEntity), operation))
        {
            return true;
        }

        var user = await AuthenticationService.GetCurrentUserAsync();
        return (await AuthorizationService.AuthorizeAsync(user, Permissions.Policy(EntityPermissions.Name(typeof(TEntity), operation)))).Succeeded;
    }

    /// <summary>Throws when the caller lacks the permission for the operation: unauthenticated or forbidden.</summary>
    protected async Task RequirePermissionAsync(string operation)
    {
        if (await HasPermissionAsync(operation))
        {
            return;
        }

        var user = await AuthenticationService.GetCurrentUserAsync();
        throw user.IsAuthenticated() ? new ForbidDocumentException($"No permission {EntityPermissions.Name(typeof(TEntity), operation)}") : new UnauthenticatedException();
    }

    /// <summary>
    /// Tells whether the caller may do the operation on the row: the permission of the table and the access list of the row (of its master for
    /// a detail), through <see cref="IAuthorizationService"/> with the row as the resource (the handler of the server applies the rule of
    /// <see cref="EntityAcl"/>). Without an authorization service (a storage built by hand) the access list alone decides. A probe only asks:
    /// its refusal is not recorded in the audit trail.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="probe">True when the caller only asks.</param>
    /// <param name="acl">The access list of the row (of the master of a detail) when it is already loaded.</param>
    /// <param name="master">The master of a detail when it is already loaded.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    protected async Task<bool> MayAsync(TEntity row, string operation, bool probe = false, IReadOnlyList<AclEntry>? acl = null, IAclEntity? master = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        var user = await AuthenticationService.GetCurrentUserAsync();
        if (AuthorizationService is not null)
        {
            object resource = probe || acl is not null || master is not null ? new EntityProbe(row, acl, Quiet: probe, Master: master) : row;
            return (await AuthorizationService.AuthorizeAsync(user, resource, Permissions.Policy(EntityPermissions.Name(typeof(TEntity), operation)))).Succeeded;
        }

        var descriptor = EntityPermissions.Of(typeof(TEntity));
        switch (descriptor.Kind)
        {
            case EntityAccessKind.Table:
                return true;
            case EntityAccessKind.Detail:
                var of = master ?? (row is IAclDetail detail ? await GetMasterAsync(detail, cancellationToken) : null);
                return of is not null && EntityAcl.RowAllows(descriptor, acl ?? await GetAclAsync(of, cancellationToken), of.OwnerId, user, operation);
            default:
                return EntityAcl.RowAllows(descriptor, acl ?? await GetAclAsync(row, cancellationToken), ((IAclEntity)row).OwnerId, user, operation);
        }
    }

    /// <summary>
    /// Reads the access list of a row (or of the master of a detail) for <see cref="MayAsync"/> without an authorization service; a storage
    /// without lists has none.
    /// </summary>
    protected virtual Task<IReadOnlyList<AclEntry>> GetAclAsync(IEntity row, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AclEntry>>([]);
    }

    /// <summary>
    /// Reads the master of a detail for <see cref="MayAsync"/> without an authorization service; a storage without masters has none.
    /// </summary>
    protected virtual Task<IAclEntity?> GetMasterAsync(IAclDetail detail, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IAclEntity?>(null);
    }

    /// <summary>
    /// Checks that the caller may update and returns the persisted entity; reading applies read authorization, so a missing and an unreadable
    /// entity are the same.
    /// </summary>
    protected virtual async Task<TEntity> AuthorizeUpdateAsync(TEntity newEntity, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();
        await RequirePermissionAsync(EntityOperations.Update);

        var key = GetKey(newEntity);
        return await GetByIdAsync(key, cancellationToken) ?? throw new NotFoundException(key);
    }

    /// <summary>Checks that the caller may delete the entity with the key and returns it.</summary>
    protected virtual async Task<TEntity> AuthorizeDeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();
        await RequirePermissionAsync(EntityOperations.Delete);

        return await GetByIdAsync(key, cancellationToken) ?? throw new NotFoundException(key);
    }

    private async Task EnsureAuthenticatedAsync()
    {
        var user = await AuthenticationService.GetCurrentUserAsync();
        if (!user.IsAuthenticated())
        {
            throw new UnauthenticatedException();
        }
    }

    /// <summary>Validates the entity with the validator, if there is one.</summary>
    protected virtual async Task ValidateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (Validator != null)
        {
            await Validator.ValidateAndThrowAsync(entity, cancellationToken);
        }
    }

    /// <summary>Returns the id of the current user.</summary>
    protected async Task<string> GetUserIdAsync()
    {
        var principal = await AuthenticationService.GetCurrentUserAsync();
        return principal.GetUserId();
    }
}
