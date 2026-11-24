namespace Flox.Shared.Services;

/// <summary>Whether the application registered the storage of an entity: the bookmarks and the recent pages exist only where it chose a store for them.</summary>
public static class EntityStorageAvailability
{
    /// <summary>Whether a storage of <typeparamref name="TEntity"/> with a <see cref="Guid"/> key is registered.</summary>
    public static bool HasStorage<TEntity>(this IServiceProviderIsService services) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.IsService(typeof(IStorage<Guid, TEntity>));
    }

    /// <summary>Whether a storage of <typeparamref name="TEntity"/> with a <see cref="Guid"/> key is registered in the provider.</summary>
    public static bool HasStorage<TEntity>(this IServiceProvider provider) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.GetService<IServiceProviderIsService>()?.HasStorage<TEntity>() ?? provider.GetService<IStorage<Guid, TEntity>>() is not null;
    }
}
