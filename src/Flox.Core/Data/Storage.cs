using System.Linq.Expressions;
using Flox.Core.Authorization;
using Flox.Core.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Data;

public static class KeyUtil
{
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
    protected StorageBase(IAuthenticationService authenticationService, ILogger<StorageBase<TKey, TEntity>> logger, IAuthorizationService? authorizationService = null, IValidator<TEntity>? validator = null)
    {
        AuthenticationService = authenticationService;
        Logger = logger;
        AuthorizationService = authorizationService;
        Validator = validator;
    }

    protected IAuthorizationService? AuthorizationService { get; }
    protected IAuthenticationService AuthenticationService { get; }
    protected ILogger Logger { get; }
    protected IValidator<TEntity>? Validator { get; }
    public abstract Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    public abstract Task DeleteAsync(TKey key, CancellationToken cancellationToken = default);

    public abstract Task<long> DeleteManyAsync(Expression<Func<TEntity, bool>> expression, int limit = Constants.MAX_BATCH_DELETE, CancellationToken cancellationToken = default);

    public abstract Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default);

    public abstract Task<PagedResult<TEntity>> FindAsync(PagedRequest<TEntity> request, CancellationToken cancellationToken = default);

    public abstract Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default);

    public abstract Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    public virtual Task<TEntity> SetAccessAsync(TKey key, AccessGrant readAccess, AccessGrant writeAccess, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} does not change the access of a document.");
    }

    public abstract Task<TEntity> UpdateAsync(TKey key, TEntity value, CancellationToken cancellationToken = default);

    public static void EnsureSameKey(TKey key, TKey entityKey)
    {
        KeyUtil.EnsureSameKey(key, entityKey);
    }

    protected TKey GetKey(TEntity entity)
    {
        return entity.Uid;
    }

    protected virtual async Task AuthorizeInsertAsync(TEntity newValue)
    {
        await EnsureAuthenticatedAsync();
        await RequirePermissionAsync(EntityOperations.Create);
    }

    public virtual Task<IReadOnlyList<EntityCapability<TKey>>> GetCapabilitiesAsync(IReadOnlyCollection<TKey> keys, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{GetType().Name} does not tell what the caller may do with a document.");
    }

    // The first layer: may the role use the table at all (Orchard's permissions of the entity); the rights of the row are the second
    protected async Task<bool> HasPermissionAsync(string operation)
    {
        if (AuthorizationService is null || !EntityPermissions.IsChecked(typeof(TEntity), operation))
        {
            return true;
        }

        var user = await AuthenticationService.GetCurrentUserAsync();
        return (await AuthorizationService.AuthorizeAsync(user, Permissions.Policy(EntityPermissions.Name(typeof(TEntity), operation)))).Succeeded;
    }

    protected async Task RequirePermissionAsync(string operation)
    {
        if (await HasPermissionAsync(operation))
        {
            return;
        }

        var user = await AuthenticationService.GetCurrentUserAsync();
        throw user.IsAuthenticated() ? new ForbidDocumentException($"No permission {EntityPermissions.Name(typeof(TEntity), operation)}") : new UnauthenticatedException();
    }

    // Returns the persisted entity: GetByIdAsync applies read authorization, so a missing and an unreadable entity are the same
    protected virtual async Task<TEntity> AuthorizeUpdateAsync(TEntity newEntity, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();
        await RequirePermissionAsync(EntityOperations.Update);

        var key = GetKey(newEntity);
        return await GetByIdAsync(key, cancellationToken) ?? throw new NotFoundException(key);
    }

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

    protected virtual async Task ValidateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (Validator != null)
        {
            await Validator.ValidateAndThrowAsync(entity, cancellationToken);
        }
    }

    protected async Task<string> GetUserIdAsync()
    {
        var principal = await AuthenticationService.GetCurrentUserAsync();
        return principal.GetUserId();
    }
}
