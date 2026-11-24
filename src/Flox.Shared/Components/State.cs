using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Flox.Shared.Errors;
using Microsoft.JSInterop;

namespace Flox.Shared.Components;

/// <summary>
/// The state of a page or a component, outside the component itself: it survives while the page is left and opened again, and notifies the page when it changes. Properties marked <c>[Inject]</c> are filled from the service provider.
/// </summary>
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

    /// <summary>
    /// Creates the state and resolves the common services from the provider.
    /// </summary>
    protected State(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        InjectProperties(serviceProvider);

        Logger = serviceProvider.GetRequiredService<ILogger<State>>();
        NavigationManager = serviceProvider.GetRequiredService<NavigationManager>();
        NavigationHistoryService = serviceProvider.GetRequiredService<INavigationHistoryService>();
        JSRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
        Notifications = serviceProvider.GetRequiredService<FloxSnackbar>();
        DialogService = serviceProvider.GetRequiredService<FloxDialogService>();
        AuthenticationService = serviceProvider.GetRequiredService<IAuthenticationService>();
        UiErrors = serviceProvider.GetRequiredService<IUiExceptionHandler>();
    }

    /// <summary>
    /// Raised when something the page shows changed.
    /// </summary>
    public event EventHandler? Changed;

    // The scroll position of the page: kept with the state, so a page that is left and opened again is where it was (see ScrollMemory)
    /// <summary>
    /// The scroll position of the page, kept with the state so that a page that is left and opened again is where it was.
    /// </summary>
    [IgnoreDataMember]
    public ScrollMemory Scroll { get; } = new();

    /// <summary>
    /// The JavaScript runtime.
    /// </summary>
    [IgnoreDataMember]
    protected IJSRuntime JSRuntime { get; }

    /// <summary>
    /// The logger of the state.
    /// </summary>
    [IgnoreDataMember]
    protected ILogger<State> Logger { get; }

    /// <summary>
    /// The navigation history of the application.
    /// </summary>
    [IgnoreDataMember]
    protected INavigationHistoryService NavigationHistoryService { get; }

    /// <summary>
    /// The service provider the state was created with.
    /// </summary>
    [IgnoreDataMember]
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// The authentication service of the application.
    /// </summary>
    [IgnoreDataMember]
    public IAuthenticationService AuthenticationService { get; }

    /// <summary>
    /// The dialog service.
    /// </summary>
    [IgnoreDataMember]
    public FloxDialogService DialogService { get; }

    /// <summary>
    /// Whether there is an error to show: a message for the user or an exception.
    /// </summary>
    [IgnoreDataMember]
    public bool Error
    {
        get
        {
            return UserError != null || Exception != null;
        }
    }

    /// <summary>
    /// The exception to show instead of the page; setting it ends the busy state.
    /// </summary>
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

    /// <summary>
    /// The navigation manager of the application.
    /// </summary>
    [IgnoreDataMember]
    public NavigationManager NavigationManager { get; }

    /// <summary>
    /// Whether what the page shows does not exist; the page then shows a not-found message.
    /// </summary>
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

    /// <summary>
    /// The notification service that shows snack messages.
    /// </summary>
    [IgnoreDataMember]
    public FloxSnackbar Notifications { get; }

    /// <summary>
    /// The handler that shows exceptions to the user.
    /// </summary>
    [IgnoreDataMember]
    public IUiExceptionHandler UiErrors { get; }

    /// <summary>
    /// The message for the user to show instead of the page.
    /// </summary>
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

    /// <summary>
    /// Whether the state is loading or working; the page shows a busy indicator.
    /// </summary>
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
    /// <param name="action">The work to run.</param>
    /// <param name="operation">Name for the log; the calling method by default.</param>
    /// <param name="caller">The calling method, filled in by the compiler.</param>
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

    /// <summary>
    /// Goes back in the navigation history when it is possible.
    /// </summary>
    protected virtual void GoBack()
    {
        if (NavigationHistoryService.CanGoBack)
        {
            NavigationHistoryService.GoBack();
        }
    }

    /// <summary>
    /// Opens the not-found page.
    /// </summary>
    protected void NotifyNotFound()
    {
        NavigationManager.NotFound();
    }

    /// <summary>
    /// Raises <see cref="Changed"/>.
    /// </summary>
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

    /// <summary>
    /// Called by a page with the key of what it shows (route values as a record): the same key again resumes, another key starts over.
    /// </summary>
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

    /// <summary>
    /// Called when the state is started for another key: clears what the page shows of the previous key. Runs before anything is awaited, so the next render has none of it.
    /// </summary>
    protected virtual void OnKeyChanged(object? newKey)
    {
    }

    /// <summary>
    /// True from the change of the key until <see cref="OnStartAsync"/> has finished: the page does not show the data of the previous key meanwhile.
    /// </summary>
    [IgnoreDataMember]
    public bool Activating { get; private set; }

    /// <summary>
    /// Loads what the page shows for the key; called when the key is new.
    /// </summary>
    protected virtual Task OnStartAsync(object? key)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the page is shown again with the same key.
    /// </summary>
    protected virtual Task OnResumeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// A pull down at the top of a touch screen refreshes what this state shows (<see cref="OnPullRefreshAsync"/>): a list does, a form never does.
    /// </summary>
    [IgnoreDataMember]
    public virtual bool RefreshesOnPull => false;

    /// <summary>
    /// Refreshes what the state shows after a pull down, keeping the search and the selection; the page itself is not loaded again.
    /// </summary>
    public virtual Task OnPullRefreshAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources of the state; a derived state overrides it.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore()
           .ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the asynchronous resources of the state; a derived state overrides it.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore()
    {
        return ValueTask.CompletedTask;
    }
}
