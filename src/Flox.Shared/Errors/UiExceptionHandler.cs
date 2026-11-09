using System.Globalization;
using System.Reflection;

namespace Flox.Shared.Errors;

/// <summary>
/// <see cref="IUiExceptionHandler"/>: runs the filters, then falls back to a standard error message.
/// </summary>
/// <remarks>
/// The standard message is the generic text plus a reference id; the exception text is added only in the
/// Development environment, because messages may carry patient data. An identical exception repeated within
/// <see cref="s_duplicateWindow"/> is logged but not shown again, so a failing timer cannot flood the screen.
/// </remarks>
[FloxService(typeof(IUiExceptionHandler))]
public sealed class UiExceptionHandler : IUiExceptionHandler
{
    private static readonly TimeSpan s_duplicateWindow = TimeSpan.FromSeconds(2);

    private readonly IUiExceptionFilter[] _filters;
    private readonly IUiNotifier _notifier;
    private readonly IFloxRuntime _runtime;
    private readonly IServiceProvider _services;
    private readonly ILogger<UiExceptionHandler> _logger;

    private string? _lastKey;
    private long _lastTicks;

    public UiExceptionHandler(IEnumerable<IUiExceptionFilter> filters, IUiNotifier notifier, IFloxRuntime runtime, IServiceProvider services, ILogger<UiExceptionHandler> logger)
    {
        _filters = [.. filters.OrderBy(filter => filter.Order)];
        _notifier = notifier;
        _runtime = runtime;
        _services = services;
        _logger = logger;
    }

    public async Task<UiExceptionContext> HandleAsync(Exception exception, string? operation = null, UiExceptionSource source = UiExceptionSource.Operation, string? userMessage = null)
    {
        var context = new UiExceptionContext(Unwrap(exception), source, operation, userMessage, _services);
        try
        {
            if (source != UiExceptionSource.Boundary && IsRepeat(context.Exception))
            {
                context.Handled = true;
                context.LogLevel = LogLevel.Debug;
            }
            else
            {
                await RunFiltersAsync(context);
                if (!context.Handled && source != UiExceptionSource.Boundary)
                {
                    ShowStandardMessage(context);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handling of UI exception {CorrelationId} failed", context.CorrelationId);
        }

        _logger.Log(context.LogLevel, context.Exception, "UI exception {CorrelationId} in {Operation} ({Source}, handled: {Handled})", context.CorrelationId, context.Operation, context.Source, context.Handled);
        return context;
    }

    public void Fire(Func<Task> action, string? operation = null)
    {
        _ = FireAsync(action, operation);
    }

    private async Task FireAsync(Func<Task> action, string? operation)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await HandleAsync(ex, operation, UiExceptionSource.Background);
        }
    }

    // A filter that fails must not hide the original exception: it is logged and the chain goes on
    private async Task RunFiltersAsync(UiExceptionContext context)
    {
        foreach (var filter in _filters)
        {
            try
            {
                await filter.HandleAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                                 "UI exception filter {Filter} failed",
                                 filter.GetType()
                                       .Name);
            }

            if (context.Handled)
            {
                return;
            }
        }
    }

    private void ShowStandardMessage(UiExceptionContext context)
    {
        var text = context.UserMessage ?? Strings.Caption_ErrorNotification;
        var reference = string.Format(CultureInfo.CurrentCulture, Strings.Error_Reference, context.CorrelationId);
        var message = $"{text} {reference}";
        if (_runtime.IsDevelopment)
        {
            message += $" [{context.Exception.GetType().Name}: {context.Exception.Message}]";
        }

        _notifier.Error(message);
    }

    private bool IsRepeat(Exception exception)
    {
        var key = $"{exception.GetType().FullName}|{exception.Message}";
        var now = Environment.TickCount64;
        var repeat = key == _lastKey && now - _lastTicks < s_duplicateWindow.TotalMilliseconds;
        _lastKey = key;
        _lastTicks = now;
        return repeat;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (true)
        {
            var inner = exception switch
            {
                AggregateException { InnerExceptions: [var single] } => single,
                TargetInvocationException { InnerException: { } target } => target,
                var _ => null
            };
            if (inner is null)
            {
                return exception;
            }

            exception = inner;
        }
    }
}
