using Microsoft.Extensions.DependencyInjection;

namespace Flox.Shared.Services;

/// <summary>Lets a component read again what it shows of rows kept on the device (bookmarks, recent pages) when another tab changes them; the lists of a data source state do it by themselves.</summary>
public static class LocalChanges
{
    /// <summary>Calls <paramref name="reload"/> when another tab changes the rows of <typeparamref name="TEntity"/>. Dispose the result with the component.</summary>
    public static IDisposable Watch<TEntity>(IServiceProvider services, Func<Task> reload) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(reload);
        return services.GetService<IStorage<Guid, TEntity>>() is ILocalChangeNotifier notifier ? new Subscription(notifier, reload) : new Subscription(null, null);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ILocalChangeNotifier? _notifier;
        private readonly Func<Task>? _reload;

        public Subscription(ILocalChangeNotifier? notifier, Func<Task>? reload)
        {
            _notifier = notifier;
            _reload = reload;
            _notifier?.ChangedElsewhere += OnChanged;
        }

        public void Dispose()
        {
            _notifier?.ChangedElsewhere -= OnChanged;
        }

        private void OnChanged(object? sender, EventArgs e)
        {
            _ = _reload!();
        }
    }
}
