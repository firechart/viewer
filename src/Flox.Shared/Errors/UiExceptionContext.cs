namespace Flox.Shared.Errors;

/// <summary>
/// Where in the UI an exception was caught.
/// </summary>
public enum UiExceptionSource
{
    /// <summary>An operation the UI started and awaited: an event handler, <c>RunAsync</c>, <c>TryAsync</c>.</summary>
    Operation,

    /// <summary>Fire-and-forget work (<see cref="IUiExceptionHandler.Fire"/>): nobody awaits it, so nobody else would see the error.</summary>
    Background,

    /// <summary>
    /// A lifecycle or rendering failure that reached an error boundary. The boundary shows its own error page,
    /// so no notification is raised for it.
    /// </summary>
    Boundary
}

/// <summary>
/// One exception travelling through the filters of <see cref="IUiExceptionHandler"/>, the client counterpart of
/// the exception context of an ASP.NET Core exception filter.
/// </summary>
public sealed class UiExceptionContext
{
    public UiExceptionContext(Exception exception, UiExceptionSource source, string? operation, string? userMessage, IServiceProvider services)
    {
        Exception = exception;
        Source = source;
        Operation = operation;
        UserMessage = userMessage;
        Services = services;
    }

    /// <summary>The exception, with wrapper exceptions (<see cref="AggregateException"/>) already removed.</summary>
    public Exception Exception { get; }

    public UiExceptionSource Source { get; }

    /// <summary>Name of the operation that failed (a method or component name), for logs.</summary>
    public string? Operation { get; }

    /// <summary>Text the caller wants the user to see instead of the standard one.</summary>
    public string? UserMessage { get; }

    /// <summary>Short id shown to the user and written to the log, so a report can be matched with the log entry.</summary>
    public string CorrelationId { get; } = Guid.NewGuid()
                                               .ToString("N")[..8];

    public IServiceProvider Services { get; }

    /// <summary>
    /// Set by a filter that dealt with the exception: the remaining filters and the standard error message are skipped.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Level of the log entry. An expected condition (a cancelled operation, a missing permission) should lower it.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Error;
}
