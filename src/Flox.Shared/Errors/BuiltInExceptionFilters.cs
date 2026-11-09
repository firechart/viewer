namespace Flox.Shared.Errors;

/// <summary>
/// A cancelled operation (the user left the page, a request was aborted) is normal: nothing to show.
/// </summary>
[FloxService(typeof(IUiExceptionFilter), ServiceLifetime.Scoped, Multiple = true)]
public sealed class CancellationExceptionFilter : IUiExceptionFilter
{
    public int Order
    {
        get
        {
            return -1000;
        }
    }

    public ValueTask HandleAsync(UiExceptionContext context)
    {
        if (context.Exception is OperationCanceledException)
        {
            context.Handled = true;
            context.LogLevel = LogLevel.Debug;
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The session ended or the token could not be renewed: send the user to sign in. Not while the identity provider is unreachable:
/// the session is kept, the banner of the provider says so and a sign-in would only fail.
/// </summary>
[FloxService(typeof(IUiExceptionFilter), ServiceLifetime.Scoped, Multiple = true)]
public sealed class UnauthenticatedExceptionFilter
(
    NavigationManager navigation,
    IAuthenticationService authentication,
    IServiceProvider services
) : IUiExceptionFilter
{
    public int Order
    {
        get
        {
            return -900;
        }
    }

    public async ValueTask HandleAsync(UiExceptionContext context)
    {
        if (context.Exception is not UnauthenticatedException)
        {
            return;
        }

        context.Handled = true;
        context.LogLevel = LogLevel.Information;
        if (services.GetService<IIdentityProviderStatus>() is { IsUnavailable: true })
        {
            return;
        }

        await authentication.LoginAsync(Uri.EscapeDataString(navigation.Uri));
    }
}

/// <summary>
/// The user is signed in but not allowed to do this.
/// </summary>
[FloxService(typeof(IUiExceptionFilter), ServiceLifetime.Scoped, Multiple = true)]
public sealed class ForbiddenExceptionFilter
(
    IUiNotifier notifier
) : IUiExceptionFilter
{
    public int Order
    {
        get
        {
            return -800;
        }
    }

    public ValueTask HandleAsync(UiExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
        {
            context.Handled = true;
            context.LogLevel = LogLevel.Warning;
            notifier.Warning(context.UserMessage ?? Strings.Error_AccessDenied);
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Data conditions the user can understand and act on (missing or duplicate record): the message is written for users.
/// </summary>
[FloxService(typeof(IUiExceptionFilter), Multiple = true)]
public sealed class DataExceptionFilter
(
    IUiNotifier notifier
) : IUiExceptionFilter
{
    public int Order
    {
        get
        {
            return -700;
        }
    }

    public ValueTask HandleAsync(UiExceptionContext context)
    {
        if (context.Exception is NotFoundException or DuplicateException or ConcurrentUpdateException)
        {
            context.Handled = true;
            context.LogLevel = LogLevel.Warning;
            notifier.Warning(context.UserMessage ?? context.Exception.Message);
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The server could not be reached at all (no status code): a connection problem, not a bug.
/// </summary>
[FloxService(typeof(IUiExceptionFilter), Multiple = true)]
public sealed class NetworkExceptionFilter
(
    IUiNotifier notifier
) : IUiExceptionFilter
{
    public int Order
    {
        get
        {
            return -600;
        }
    }

    public ValueTask HandleAsync(UiExceptionContext context)
    {
        if (context.Exception is HttpRequestException { StatusCode: null })
        {
            context.Handled = true;
            context.LogLevel = LogLevel.Warning;
            notifier.Warning(context.UserMessage ?? Strings.Error_NetworkUnavailable);
        }

        return ValueTask.CompletedTask;
    }
}
