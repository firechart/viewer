using Microsoft.Extensions.DependencyInjection;

namespace Flox.Core.Data;

/// <summary>Registration of data sources.</summary>
public static class DataSourceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the data source of a <see cref="IGuidEntity"/>; the closed generics are named statically because AOT (iOS) cannot instantiate
    /// them from <c>MakeGenericType</c>.
    /// </summary>
    public static IServiceCollection AddDataSource<TEntity>(this IServiceCollection services) where TEntity : IGuidEntity
    {
        services.AddTransient<GuidDataSource<TEntity>>();
        services.AddTransient<IGuidDataSource<TEntity>>(sp => sp.GetRequiredService<GuidDataSource<TEntity>>());
        services.AddTransient<IDataSource<Guid, TEntity>>(sp => sp.GetRequiredService<GuidDataSource<TEntity>>());
        return services;
    }

    /// <summary>Registers the data source of an entity with the given key type.</summary>
    public static IServiceCollection AddDataSource<TKey, TEntity>(this IServiceCollection services) where TKey : notnull
    {
        services.AddTransient<DataSource<TKey, TEntity>>();
        services.AddTransient<IDataSource<TKey, TEntity>>(sp => sp.GetRequiredService<DataSource<TKey, TEntity>>());
        return services;
    }
}
