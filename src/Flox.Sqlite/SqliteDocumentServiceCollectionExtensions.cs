using Flox.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Flox.Sqlite;

/// <summary>Registration of the document store of the device over SQLite.</summary>
public static class SqliteDocumentServiceCollectionExtensions
{
    /// <summary>The base of the core of Flox: the settings of the client, bookmarks, recent pages and the other documents of the device.</summary>
    public const string CoreDatabase = "flox.sqlite";

    /// <summary>
    /// Registers the SQLite of the browser (<see cref="BrowserSqliteEngine"/>) in the pool of the application: every application owns its pool,
    /// whatever origin it shares with others.
    /// </summary>
    public static IServiceCollection AddBrowserSqlite(this IServiceCollection services, string pool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pool);
        return services.AddSingleton<ISqliteEngine>(sp => new BrowserSqliteEngine(sp.GetRequiredService<IJSRuntime>(), pool));
    }

    /// <summary>
    /// Registers the documents of the device in a base of the <see cref="ISqliteEngine"/> of the host: the <see cref="ILocalDocumentStore"/> of
    /// the application without a key, a base of a module under its key (<c>AddLocalEntity</c> with the same key puts an entity there).
    /// </summary>
    public static IServiceCollection AddSqliteDocuments(this IServiceCollection services, string database = CoreDatabase, string? key = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        if (key is null)
        {
            return services.AddSingleton<ILocalDocumentStore>(sp => new SqliteDocumentStore(sp.GetRequiredService<ISqliteEngine>(), database));
        }

        return services.AddKeyedSingleton<ILocalDocumentStore>(key, (sp, _) => new SqliteDocumentStore(sp.GetRequiredService<ISqliteEngine>(), database));
    }
}
