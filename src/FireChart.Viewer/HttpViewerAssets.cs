using System.Net;
using System.Net.Mime;
using FireChart.Viewer.Core;
using Flox.Core.Host;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer;

/// <summary>
/// The <see cref="IViewerAssets"/> of a browser: a GET of the path relative to the base address of the page, through the HTTP cache of the browser.
/// A 404 is a missing file, and so is an HTML page: a host that answers every path with its page (the fallback of a single-page app,
/// <c>try_files ... /index.html</c> of nginx) has no such file either; scoped.
/// </summary>
/// <param name="httpClientFactory">Makes the client of the request.</param>
/// <param name="navigationManager">Knows the base address of the page.</param>
/// <example>
/// <code>
/// var text = await assets.ReadTextAsync("_content/FireChart.Viewer.Library/search-forms/r4/Patient.json?v=1.2.0", cancellationToken);
/// </code>
/// </example>
[FloxService(typeof(IViewerAssets), ServiceLifetime.Scoped)]
public sealed class HttpViewerAssets(IHttpClientFactory httpClientFactory, NavigationManager navigationManager) : IViewerAssets
{
    /// <inheritdoc/>
    public async Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var http = httpClientFactory.CreateClient();
        using var response = await http.GetAsync(navigationManager.ToAbsoluteUri(path), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound || response.Content.Headers.ContentType?.MediaType == MediaTypeNames.Text.Html)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
