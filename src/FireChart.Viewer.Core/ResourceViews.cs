using FireChart.Viewer.Core.Fhir;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Core;

/// <summary>
/// The view of a resource.
/// </summary>
/// <param name="Html">What the page puts in its container, already passed through <see cref="FxHtml.Sanitize"/>.</param>
/// <param name="Style">The CSS of the kit <c>fx-*</c> in force.</param>
/// <example>
/// <code>
/// var view = await views.RenderAsync(resource, related, "R4", link =&gt; null);
/// if (view is not null)
/// {
///     markup = view.Html;
/// }
/// </code>
/// </example>
public sealed record ResourceView(string Html, string Style);

/// <summary>
/// The page of a resource: the template of the mode <c>view</c> of its type, with the resources that came with it (the patient, the encounter).
/// </summary>
/// <example>
/// <code>
/// var needs = await views.NeedsAsync("Encounter");
/// var view = await views.RenderAsync(resource, related, "R4", ResolveLink, server: server);
/// </code>
/// </example>
public interface IResourceViews
{
    /// <summary>The mode of the page of a resource: <c>view</c>.</summary>
    public const string Mode = "view";

    /// <summary>The mode of the header of a patient or an encounter above other things: <c>banner</c>.</summary>
    public const string BannerMode = "banner";

    /// <summary>
    /// What the view template of a type asks the server to send with the resource.
    /// </summary>
    /// <param name="resourceType">The type.</param>
    /// <param name="cancellationToken">Cancels the loading of the templates.</param>
    /// <returns>The needs; none when there is no template or the templates are not available.</returns>
    Task<FhirNeeds> NeedsAsync(string resourceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a resource in a mode.
    /// </summary>
    /// <param name="resource">The resource, with its JSON.</param>
    /// <param name="related">The resources that came with it.</param>
    /// <param name="release">The release of the server it comes from; R4 when not given.</param>
    /// <param name="resolveLink">The address of the application for a link <c>fhir:Type/id</c>; <see langword="null"/> drops the link.</param>
    /// <param name="mode">The mode (<see cref="Mode"/>, <see cref="BannerMode"/>).</param>
    /// <param name="terminologyServer">The terminology server for the displays of bare codes; none: the codes are shown as they are.</param>
    /// <param name="server">The server the patient and the encounter the resource refers to are fetched from when they did not come with it.</param>
    /// <param name="cancellationToken">Cancels the rendering.</param>
    /// <returns>The view, or <see langword="null"/> when the type has no template of the mode or it cannot be rendered (the page shows the JSON).</returns>
    Task<ResourceView?> RenderAsync(ResourceSummary resource, IReadOnlyList<RelatedResource> related, string? release, Func<string, string?> resolveLink, string mode = Mode, string? terminologyServer = null,
                                    IViewerServer? server = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IResourceViews"/> over an <see cref="ITemplateRenderers"/>; scoped.
/// </summary>
/// <param name="renderers">The renderer in force.</param>
/// <param name="models">The models of the releases.</param>
/// <param name="logger">Logs a view that cannot be rendered (the type and the id).</param>
/// <param name="terminology">The displays of codes; none: the codes are shown as they are.</param>
/// <param name="references">Fetches the patient and the encounter the view misses; none: they are not fetched.</param>
/// <example>
/// <code>
/// var views = new ResourceViews(renderers, models, loggerFactory.CreateLogger&lt;ResourceViews&gt;());
/// </code>
/// </example>
[FloxService(typeof(IResourceViews), ServiceLifetime.Scoped)]
public sealed class ResourceViews(ITemplateRenderers renderers, IReleaseModels models, ILogger<ResourceViews> logger, ITerminologyResolver? terminology = null, IReferenceFetcher? references = null) : IResourceViews
{
    private static Task<string> RenderOnceAsync(FhirRenderer renderer, string json, string mode, Hl7.Fhir.Introspection.ModelInspector model, ResourcePool pool)
    {
        return mode == IResourceViews.Mode ? renderer.RenderViewAsync(json, mode, model, pool) : renderer.RenderAsync(json, mode, model, pool);
    }

    /// <inheritdoc/>
    public async Task<FhirNeeds> NeedsAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        try
        {
            return (await renderers.GetAsync(cancellationToken)).NeedsFor(resourceType, IResourceViews.Mode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The templates are not available, nothing is asked to come with {Type}", resourceType);
            return FhirNeeds.None;
        }
    }

    /// <inheritdoc/>
    public async Task<ResourceView?> RenderAsync(ResourceSummary resource, IReadOnlyList<RelatedResource> related, string? release, Func<string, string?> resolveLink, string mode = IResourceViews.Mode, string? terminologyServer = null,
                                                 IViewerServer? server = null, CancellationToken cancellationToken = default)
    {
        if (resource.Json is null)
        {
            return null;
        }

        try
        {
            var renderer = await renderers.GetAsync(cancellationToken);
            if (!renderer.HasTemplate(resource.ResourceType, mode))
            {
                return null;
            }

            var model = await models.GetAsync(release, cancellationToken);
            var pool = FhirRenderer.CreatePool([new PoolEntry(resource.Json), .. related.Select(item => new PoolEntry(item.Json, item.FullUrl))], model);
            await TerminologyPool.AttachAsync(terminology, pool, terminologyServer, [resource.Json, .. related.Select(item => item.Json)], TimeSpan.FromSeconds(2.5), cancellationToken);
            var html = await RenderOnceAsync(renderer, resource.Json, mode, model, pool);
            if (server is not null && references is not null && pool.Misses.Count > 0)
            {
                var fetched = await references.FetchAsync(server, [.. pool.Misses], cancellationToken);
                if (fetched.Found.Count > 0 || fetched.Missing.Count > 0)
                {
                    pool.AddAll(fetched.Found.Select(item => new PoolEntry(item.Json, item.FullUrl)), model);
                    pool.Missing.UnionWith(fetched.Missing);
                    html = await RenderOnceAsync(renderer, resource.Json, mode, model, pool);
                }
            }

            return new ResourceView(FxHtml.Sanitize(html, resolveLink), await renderers.GetStyleAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The view of {Type} {Id} cannot be rendered, the page shows the JSON", resource.ResourceType, resource.Id);
            return null;
        }
    }
}
