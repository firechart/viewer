using FireChart.Viewer.Core;
using FireChart.Viewer.Library;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer;

/// <summary>
/// The CSS of the kit <c>fx-*</c> read from the files of the library the application serves (<c>styles/fx.css</c>).
/// </summary>
/// <param name="assets">The files of the library.</param>
/// <example>
/// <code>
/// services.AddScoped&lt;IViewerStyle, ViewerStyle&gt;();
/// </code>
/// </example>
[FloxService(typeof(IViewerStyle), ServiceLifetime.Scoped)]
public sealed class ViewerStyle(IViewerAssets assets) : IViewerStyle
{
    private string? _style;

    /// <inheritdoc/>
    public async Task<string> GetStyleAsync(CancellationToken cancellationToken = default)
    {
        return _style ??= (await FxStyle.LoadAsync(assets, cancellationToken)).Compose(null);
    }
}
