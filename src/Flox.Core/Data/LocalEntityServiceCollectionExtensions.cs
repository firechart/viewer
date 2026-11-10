using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flox.Core.Data;

public static class LocalEntityServiceCollectionExtensions
{
    // An entity kept on the device (LocalEntityStorage over the ILocalDocumentStore of the host) and its data source. Scoped: one copy of the rows
    // in memory per scope, which is the application in WebAssembly and MAUI. The collection is the name of the class unless given
    public static IServiceCollection AddLocalEntity<TEntity>(this IServiceCollection services, string? collection = null) where TEntity : class, IGuidEntity
    {
        services.AddScoped(sp => new LocalGuidEntityStorage<TEntity>(sp.GetRequiredService<ILocalDocumentStore>(),
                                                                     sp.GetRequiredService<ILogger<LocalEntityStorage<Guid, TEntity>>>(),
                                                                     sp.GetService<IValidator<TEntity>>(),
                                                                     collection));
        services.AddScoped<IGuidEntityStorage<TEntity>>(sp => sp.GetRequiredService<LocalGuidEntityStorage<TEntity>>());
        services.AddScoped<IStorage<Guid, TEntity>>(sp => sp.GetRequiredService<LocalGuidEntityStorage<TEntity>>());
        return services.AddDataSource<TEntity>();
    }
}
