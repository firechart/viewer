using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Flox.Shared.Errors;
using Microsoft.JSInterop;
using Radzen;

namespace Flox.Shared.Components;

[FloxService]
public abstract class State : IDisposable, IAsyncDisposable
{
    // Property injection for State-derived classes, which are plain DI-resolved POCOs rather than
    // IComponent - Blazor's own [Inject] handling lives in IComponentActivator and only fires for
    // components, so it has to be replicated by hand here. Reuses Blazor's InjectAttribute as a
    // marker only; results are cached per concrete type since GetProperties() is a reflection scan.
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_injectableProperties = new ConcurrentDictionary<Type, PropertyInfo[]>();

    private Exception? _exception;
    private object? _activationKey;
    private bool _activated;

    protected State(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        InjectProperties(serviceProvider);

        Logger = serviceProvider.GetRequiredService<ILogger<State>>();
        NavigationManager = serviceProvider.GetRequiredService<NavigationManager>();
        NavigationHistoryService = serviceProvider.GetRequiredService<INavigationHistoryService>();
        JSRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
        Notifications = serviceProvider.GetRequiredService<NotificationService>();
        DialogService = serviceProvider.GetRequiredService<DialogService>();
        AuthenticationService = serviceProvider.GetRequiredService<IAuthenticationService>();
        UiErrors = serviceProvider.GetRequiredService<IUiExceptionHandler>();
    }

    public event EventHandler? Changed;

    // The scroll position of the page: kept with the state, so a page that is left and opened again is where it was (see ScrollMemory)
    [IgnoreDataMember]
    public ScrollMemory Scroll { get; } = new();

    [IgnoreDataMember]
    protected IJSRuntime JSRuntime { get; }

    [IgnoreDataMember]
    protected ILogger<State> Logger { get; }

    [IgnoreDataMember]
    protected INavigationHistoryService NavigationHistoryService { get; }

    [IgnoreDataMember]
    protected IServiceProvider ServiceProvider { get; }

    [IgnoreDataMember]
    public IAuthenticationService AuthenticationService { get; }

    [IgnoreDataMember]
    public DialogService DialogService { get; }

    [IgnoreDataMember]
    public bool Error
    {
        get
        {
            return UserError != null || Exception != null;
        }
    }

    [IgnoreDataMember]
    public Exception? Exception
    {
        get
        {
            return _exception;
        }

        set
        {
            if (_exception != value)
            {
                _exception = value;
                if (value != null)
                {
                    IsBusy = false;
                }

                NotifyChanged();
            }
        }
    }

    [IgnoreDataMember]
    public NavigationManager NavigationManager { get; }

    [IgnoreDataMember]
    public bool NotFound
    {
        get;
        set
        {
            if (field != value)
            {
                _exception = null;
                field = value;
                IsBusy = false;
                NotifyChanged();
            }
        }
    }

    [IgnoreDataMember]
    public NotificationService Notifications { get; }

    [IgnoreDataMember]
    public IUiExceptionHandler UiErrors { get; }

    [IgnoreDataMember]
    public string? UserError
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyChanged();
            }
        }
    }

    [IgnoreDataMember]
    public virtual bool IsBusy
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyChanged();
            }
        }
    } = true;

    /// <summary>
    /// Runs <paramref name="action"/> with <see cref="IsBusy"/> set and shows what it throws through <see cref="UiErrors"/>.
    /// </summary>
    /// <param name="operation">Name for the log; the calling method by default.</param>
    /// <returns><c>true</c> when it completed, <c>false</c> when it failed.</returns>
    protected async Task<bool> RunAsync(Func<Task> action, string? operation = null, [CallerMemberName] string? caller = null)
    {
        IsBusy = true;
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            await UiErrors.HandleAsync(ex, operation ?? caller);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected virtual void GoBack()
    {
        if (NavigationHistoryService.CanGoBack)
        {
            NavigationHistoryService.GoBack();
        }
    }

    protected void NotifyNotFound()
    {
        NavigationManager.NotFound();
    }

    protected void NotifyChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void InjectProperties(IServiceProvider serviceProvider)
    {
        // Initializes properties marked with [Inject]
        var properties = s_injectableProperties.GetOrAdd(GetType(), FindInjectableProperties);
        foreach (var property in properties)
        {
            property.SetValue(this, serviceProvider.GetRequiredService(property.PropertyType));
        }
    }

    private static PropertyInfo[] FindInjectableProperties(Type type)
    {
        // Finds properties marked with [Inject]
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             .Where(p => p.IsDefined(typeof(InjectAttribute), true))
                             .ToArray();

        var readOnly = properties.FirstOrDefault(p => !p.CanWrite);
        if (readOnly != null)
        {
            throw new InvalidOperationException($"{type.Name}.{readOnly.Name} is marked [Inject] but has no setter - add a 'set' or 'init' accessor.");
        }

        return properties;
    }

    // A page calls it with the key of what it shows (route values as a record). The same key again resumes, another key starts over
    public async Task ActivateAsync(object? key)
    {
        if (_activated && Equals(_activationKey, key))
        {
            await OnResumeAsync();
            return;
        }

        // Another key: what the state holds is not what the page shows now. The page hides its content and shows the busy indicator at once (Activating),
        // not when the load has got as far as clearing the fields
        Activating = true;
        Scroll.Top = 0;
        OnKeyChanged(key);
        IsBusy = true;
        NotifyChanged();
        try
        {
            await OnStartAsync(key);
            _activationKey = key;
            _activated = true;
        }
        finally
        {
            Activating = false;
            NotifyChanged();
        }
    }

    // The state is being started for another key: clear what the page shows (the previous key's data). Runs before anything is awaited, so the next render has none of it
    protected virtual void OnKeyChanged(object? newKey)
    {
    }

    // True from the change of the key until OnStartAsync has finished: a page does not show the data of the previous key meanwhile
    [IgnoreDataMember]
    public bool Activating { get; private set; }

    protected virtual Task OnStartAsync(object? key)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnResumeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore()
           .ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsyncCore()
    {
        return ValueTask.CompletedTask;
    }
}
