using Microsoft.Extensions.DependencyInjection;

namespace Flox.Core.Data;

public static class DataSourceServiceCollectionExtensions
{
    // Closed generics are named statically: AOT (iOS) cannot instantiate them from MakeGenericType
    public static IServiceCollection AddDataSource<TEntity>(this IServiceCollection services) where TEntity : IGuidEntity
    {
        services.AddTransient<GuidDataSource<TEntity>>();
        services.AddTransient<IGuidDataSource<TEntity>>(sp => sp.GetRequiredService<GuidDataSource<TEntity>>());
        services.AddTransient<IDataSource<Guid, TEntity>>(sp => sp.GetRequiredService<GuidDataSource<TEntity>>());
        return services;
    }

    public static IServiceCollection AddDataSource<TKey, TEntity>(this IServiceCollection services) where TKey : notnull
    {
        services.AddTransient<DataSource<TKey, TEntity>>();
        services.AddTransient<IDataSource<TKey, TEntity>>(sp => sp.GetRequiredService<DataSource<TKey, TEntity>>());
        return services;
    }
}
