namespace Flox.Shared.Errors;

/// <summary>
/// The single place where the client turns an exception into something the user sees: it runs the
/// <see cref="IUiExceptionFilter"/> chain, falls back to a standard message and logs the result.
/// </summary>
public interface IUiExceptionHandler
{
    /// <summary>
    /// Handles <paramref name="exception"/>; never throws.
    /// </summary>
    /// <param name="operation">Name of the failed operation, for logs.</param>
    /// <param name="userMessage">Text to show instead of the standard one.</param>
    Task<UiExceptionContext> HandleAsync(Exception exception, string? operation = null, UiExceptionSource source = UiExceptionSource.Operation, string? userMessage = null);

    /// <summary>
    /// Runs <paramref name="action"/> without awaiting it and handles what it throws. Use it in place of
    /// <c>async void</c>, whose exceptions nothing can catch.
    /// </summary>
    void Fire(Func<Task> action, string? operation = null);
}

public static class UiExceptionHandlerExtensions
{
    /// <summary>
    /// Runs <paramref name="action"/> and handles what it throws.
    /// </summary>
    /// <returns><c>true</c> when it completed, <c>false</c> when it failed.</returns>
    public static async Task<bool> TryAsync(this IUiExceptionHandler handler, Func<Task> action, string? operation = null)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            await handler.HandleAsync(ex, operation);
            return false;
        }
    }
}
