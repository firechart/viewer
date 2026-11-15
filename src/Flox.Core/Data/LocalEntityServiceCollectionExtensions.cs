using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Data;

/// <summary>Registration of entities kept on the device.</summary>
public static class LocalEntityServiceCollectionExtensions
{
    /// <summary>
    /// Registers an entity kept on the device (local storage over the document store of the host, or the one of a module under <paramref
    /// name="storeKey" />) and its data source; scoped, so there is one copy of the rows in memory per scope. The collection is the name of the
    /// class unless given.
    /// </summary>
    public static IServiceCollection AddLocalEntity<TEntity>(this IServiceCollection services, string? collection = null, string? storeKey = null) where TEntity : class, IGuidEntity
    {
        services.AddScoped(sp => new LocalGuidEntityStorage<TEntity>(storeKey is null ? sp.GetRequiredService<ILocalDocumentStore>() : sp.GetRequiredKeyedService<ILocalDocumentStore>(storeKey),
                                                                     sp.GetRequiredService<ILogger<LocalEntityStorage<Guid, TEntity>>>(),
                                                                     sp.GetService<IValidator<TEntity>>(),
                                                                     collection));
        services.AddScoped<IGuidEntityStorage<TEntity>>(sp => sp.GetRequiredService<LocalGuidEntityStorage<TEntity>>());
        services.AddScoped<IStorage<Guid, TEntity>>(sp => sp.GetRequiredService<LocalGuidEntityStorage<TEntity>>());
        return services.AddDataSource<TEntity>();
    }
}
