using Microsoft.JSInterop;

namespace Flox.Services;

/// <summary>
/// Imports the JavaScript modules of the components and services. Now and then the interop answers an import with nothing (null) although the
/// browser loaded the module: seen with and without browser extensions (2026-11), not reproduced at will; a later import succeeded. An import is
/// tried again a few times before it fails with a clear message.
/// </summary>
public static class JSModuleImport
{
    private const int Attempts = 4;
    private static readonly TimeSpan s_pause = TimeSpan.FromMilliseconds(100);

    /// <summary>Imports the module at the path (relative to the base of the page, <c>./_content/...</c>).</summary>
    /// <exception cref="InvalidOperationException">Every attempt came back empty.</exception>
    public static async ValueTask<IJSObjectReference> ImportModuleAsync(this IJSRuntime runtime, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        for (var attempt = 1; ; attempt++)
        {
            if (await runtime.InvokeAsync<IJSObjectReference?>("import", cancellationToken, path) is { } module)
            {
                return module;
            }

            if (attempt == Attempts)
            {
                throw new InvalidOperationException($"The script {path} came back empty from the interop {Attempts} times: a browser extension may break the interop of the page.");
            }

            await Task.Delay(s_pause * attempt, cancellationToken);
        }
    }
}
