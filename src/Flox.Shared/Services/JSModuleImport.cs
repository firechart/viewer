using Microsoft.JSInterop;

namespace Flox.Services;

/// <summary>
/// Imports the JavaScript modules of the components and services. Now and then the interop of Blazor WebAssembly answers an import with null
/// although the browser loaded the module: seen with a browser extension (Urban VPN Proxy), under the debugger of Visual Studio and without either
/// (2026-11), never reproduced at will, and the same import made again succeeded. An import may run twice (the browser loads a module once), so it
/// is tried again: 4 attempts, 100, 200 and 300 ms apart, then an error that names the likely cause. The same is done for the open of a base of
/// SQLite (BrowserSqliteEngine).
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
