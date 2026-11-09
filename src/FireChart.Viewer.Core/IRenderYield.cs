namespace FireChart.Viewer.Core;

/// <summary>
/// Gives the thread back to the host for a moment during a long piece of work. In WebAssembly the .NET code runs on the thread of the browser,
/// which draws nothing (not even a busy indicator) until the code returns; elsewhere there is nothing to give back. Without an implementation the
/// work runs through.
/// </summary>
/// <example>
/// <code>
/// foreach (var item in items)
/// {
///     Render(item);
///     await renderYield.YieldAsync();
/// }
/// </code>
/// </example>
public interface IRenderYield
{
    /// <summary>
    /// Lets the host draw a frame, then continues.
    /// </summary>
    /// <returns>A task that completes when the work may go on.</returns>
    ValueTask YieldAsync();
}
