using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FireChart.Viewer.Library;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer;

/// <summary>
/// Which resource types have a summary: the ones whose component <c>{Type}.summary</c> the library has (<c>components/Patient.summary.xml</c>), in
/// a package of the host or in the vendor library. Scoped: the answers (a missing summary too) are kept for the scope.
/// </summary>
/// <example>
/// <code>
/// if (await summaries.HasSummaryAsync("Encounter"))
/// {
///     session.Navigate(new ViewerRoute(ViewerPage.Summary, "Encounter", id));
/// }
/// </code>
/// </example>
[FloxService(ServiceLifetime.Scoped)]
public sealed partial class ViewerSummaries
{
    private readonly IViewerAssets _assets;
    private readonly ConcurrentDictionary<string, Lazy<Task<bool>>> _known = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="assets">Reads the components, the static files of the library.</param>
    public ViewerSummaries(IViewerAssets assets)
    {
        _assets = assets;
    }

    /// <summary>
    /// Whether a resource type has a summary: the switch between the record and its summary shows only then.
    /// </summary>
    /// <param name="resourceType">The type of the record.</param>
    /// <param name="cancellationToken">Cancels the reading of the file.</param>
    /// <returns><see langword="true"/> when the library has the component <c>{Type}.summary</c>.</returns>
    /// <example>
    /// <code>
    /// var summary = await summaries.HasSummaryAsync(route.ResourceType);
    /// </code>
    /// </example>
    public async Task<bool> HasSummaryAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        if (!TypeRegex().IsMatch(resourceType))
        {
            return false;
        }

        var known = _known.GetOrAdd(resourceType, type => new Lazy<Task<bool>>(() => ExistsAsync(type)));
        return await known.Value.WaitAsync(cancellationToken);
    }

    private async Task<bool> ExistsAsync(string resourceType)
    {
        try
        {
            return await _assets.ReadTextAsync(VendorLibrary.UrlOf($"components/{resourceType}.summary.xml"), CancellationToken.None) is not null;
        }
        catch (HttpRequestException)
        {
            // The library cannot be read: no switch, the record still shows
            return false;
        }
    }

    [GeneratedRegex("^[A-Z][A-Za-z]{0,63}$")]
    private static partial Regex TypeRegex();
}
