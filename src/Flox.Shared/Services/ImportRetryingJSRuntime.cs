using Microsoft.JSInterop;

namespace Flox.Services;

/// <summary>
/// The JavaScript runtime of the page whose <c>import</c> of a module never answers null: it is tried again as <see cref="JSModuleImport"/> does, and
/// fails with an exception when every attempt came back empty. Libraries that import their modules themselves get it from DI: Blazor Blueprint keeps
/// the first answer of an import for the life of the page, a null too, and then every component of the bundle failed until a reload. Every other call
/// goes to the runtime of Blazor unchanged.
/// </summary>
/// <param name="inner">The runtime of Blazor WebAssembly.</param>
public sealed class ImportRetryingJSRuntime(IJSInProcessRuntime inner) : IJSInProcessRuntime
{
    private const string Import = "import";

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier == Import && typeof(TValue) == typeof(IJSObjectReference) && args is [string path])
        {
            return ImportAsync<TValue>(path, cancellationToken);
        }

        return inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
    }

    /// <inheritdoc />
    public ValueTask<IJSObjectReference> InvokeConstructorAsync(string identifier, object?[]? args)
    {
        return inner.InvokeConstructorAsync(identifier, args);
    }

    /// <inheritdoc />
    public ValueTask<IJSObjectReference> InvokeConstructorAsync(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        return inner.InvokeConstructorAsync(identifier, cancellationToken, args);
    }

    /// <inheritdoc />
    public ValueTask<TValue> GetValueAsync<TValue>(string identifier)
    {
        return inner.GetValueAsync<TValue>(identifier);
    }

    /// <inheritdoc />
    public ValueTask<TValue> GetValueAsync<TValue>(string identifier, CancellationToken cancellationToken)
    {
        return inner.GetValueAsync<TValue>(identifier, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SetValueAsync<TValue>(string identifier, TValue value)
    {
        return inner.SetValueAsync(identifier, value);
    }

    /// <inheritdoc />
    public ValueTask SetValueAsync<TValue>(string identifier, TValue value, CancellationToken cancellationToken)
    {
        return inner.SetValueAsync(identifier, value, cancellationToken);
    }

    /// <inheritdoc />
    public TResult Invoke<TResult>(string identifier, params object?[]? args)
    {
        return inner.Invoke<TResult>(identifier, args);
    }

    /// <inheritdoc />
#pragma warning disable IDISP005 // the member of the interface: the caller owns the reference
    public IJSObjectReference InvokeConstructor(string identifier, object?[]? args)
    {
        return inner.InvokeConstructor(identifier, args);
    }
#pragma warning restore IDISP005

    /// <inheritdoc />
    public TValue GetValue<TValue>(string identifier)
    {
        return inner.GetValue<TValue>(identifier);
    }

    /// <inheritdoc />
    public void SetValue<TValue>(string identifier, TValue value)
    {
        inner.SetValue(identifier, value);
    }

    private async ValueTask<TValue> ImportAsync<TValue>(string path, CancellationToken cancellationToken)
    {
        return (TValue)await inner.ImportModuleAsync(path, cancellationToken);
    }
}
