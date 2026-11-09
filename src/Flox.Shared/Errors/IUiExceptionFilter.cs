namespace Flox.Shared.Errors;

/// <summary>
/// A step of client exception handling: looks at an exception and, when it knows what to do, does it and sets
/// <see cref="UiExceptionContext.Handled"/>.
/// </summary>
/// <remarks>
/// Filters run in <see cref="Order"/> (registration order for equal values) until one handles the exception.
/// Register a filter with
/// <c>[FloxService(ServiceType = typeof(IUiExceptionFilter), Lifetime = ServiceLifetime.Scoped, Multiple = true)]</c>;
/// any assembly the bootstrapper scans can contribute filters.
/// </remarks>
public interface IUiExceptionFilter
{
    /// <summary>Lower runs first. Built-in filters use -1000 to -500; leave room to run before or after them.</summary>
    int Order
    {
        get
        {
            return 0;
        }
    }

    ValueTask HandleAsync(UiExceptionContext context);
}
