using FireChart.Viewer.Core.Fhir;
using Flox.Core.Host;
using Hl7.Fhir.Introspection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Core;

/// <summary>
/// The rows of a list of resources: the template of the mode <c>tile</c> of each type, else the id and the type.
/// </summary>
/// <example>
/// <code>
/// var needs = await tiles.NeedsAsync("Observation");
/// var page = await reader.SearchAsync(server, "Observation", search, needs);
/// var rows = await tiles.RenderAsync(page.Items, "R4", page.Related, server: server);
/// </code>
/// </example>
public interface IResourceTiles
{
    /// <summary>
    /// What the row template of a type asks the server to send with the resources of a list.
    /// </summary>
    /// <param name="resourceType">The type.</param>
    /// <param name="cancellationToken">Cancels the loading of the templates.</param>
    /// <returns>The needs; none when there is no template or the templates are not available.</returns>
    Task<FhirNeeds> NeedsAsync(string resourceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// The CSS of the kit <c>fx-*</c> (the rows with markup use its badges).
    /// </summary>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The CSS; empty when the templates are not available.</returns>
    Task<string> StyleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The rows of resources, by their ids. A resource the template cannot render shows the id and the type (<see cref="ResourceTiles.Plain"/>).
    /// </summary>
    /// <param name="items">The resources.</param>
    /// <param name="release">The release of the server they come from; R4 when not given.</param>
    /// <param name="related">What came with them; the templates reach it through a <c>Reference</c>.</param>
    /// <param name="terminologyServer">The terminology server for the displays of bare codes (<see cref="TerminologyResolver.AddressOf"/>); a list waits
    /// only a second for it, the answers that come later are used when the page draws its rows again (<see cref="ITerminologyResolver.Resolved"/>).</param>
    /// <param name="context">What the page shows above the list (the rows leave it out) and the options of the rows; none: a list of its own.</param>
    /// <param name="server">The server the patients and the encounters the rows refer to are fetched from when they did not come with them; the rows are then
    /// made again (<see cref="IReferenceFetcher"/>).</param>
    /// <param name="cancellationToken">Cancels the rendering.</param>
    /// <returns>A row for every resource, by its id.</returns>
    Task<IReadOnlyDictionary<string, TileModel>> RenderAsync(IReadOnlyList<ResourceSummary> items, string? release = null, IReadOnlyList<RelatedResource>? related = null, string? terminologyServer = null,
                                                             TileContext? context = null, IViewerServer? server = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders records through a template of slots (the cells of a row of a table: <see cref="FhirRenderer.RenderSlotsAsync"/>).
    /// </summary>
    /// <param name="items">The records.</param>
    /// <param name="template">The key of the template (<c>lab-row</c>).</param>
    /// <param name="release">The release of the server.</param>
    /// <param name="related">What the records may reach through a Reference.</param>
    /// <param name="terminologyServer">The terminology server for the display of codes.</param>
    /// <param name="context">What the page shows above and the options (the format of the section).</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The slots of each record by its id; none for a record that could not be rendered.</returns>
    /// <example>
    /// <code>
    /// var rows = await tiles.RenderSlotsAsync(observations, "lab-row", release, related, context: context);
    /// </code>
    /// </example>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> RenderSlotsAsync(IReadOnlyList<ResourceSummary> items, string template, string? release = null,
                                                                                             IReadOnlyList<RelatedResource>? related = null, string? terminologyServer = null,
                                                                                             TileContext? context = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// The <see cref="IResourceTiles"/> over an <see cref="ITemplateRenderers"/>; scoped.
/// </summary>
/// <param name="renderers">The renderer in force.</param>
/// <param name="models">The models of the releases.</param>
/// <param name="logger">Logs a row that cannot be rendered (the type and the id).</param>
/// <param name="terminology">The displays of codes; none: the codes are shown as they are.</param>
/// <param name="references">Fetches the patients and the encounters the rows miss; none: they are not fetched.</param>
/// <param name="renderYield">Gives the thread back to the browser between the rows now and then; none: the rows are rendered in one go.</param>
/// <example>
/// <code>
/// var tiles = new ResourceTiles(renderers, new ReleaseModels(new NoReleaseAssemblyLoader()), loggerFactory.CreateLogger&lt;ResourceTiles&gt;());
/// </code>
/// </example>
[FloxService(typeof(IResourceTiles), ServiceLifetime.Scoped)]
public sealed class ResourceTiles(ITemplateRenderers renderers, IReleaseModels models, ILogger<ResourceTiles> logger, ITerminologyResolver? terminology = null, IReferenceFetcher? references = null,
                                  IRenderYield? renderYield = null) : IResourceTiles
{
    // In WebAssembly the rows are rendered on the thread of the browser: after this long it gets the thread back for a frame
    private const int YieldAfterMs = 40;

    /// <summary>
    /// The row of a resource without a template: its id and its type.
    /// </summary>
    /// <param name="item">The resource.</param>
    /// <returns>The row.</returns>
    public static TileModel Plain(ResourceSummary item)
    {
        return TileModel.Plain(item.Id, item.ResourceType);
    }

    /// <inheritdoc/>
    public async Task<string> StyleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await renderers.GetStyleAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The style of the templates is not available");
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public async Task<FhirNeeds> NeedsAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        try
        {
            return await (await renderers.GetAsync(cancellationToken)).NeedsForAsync(resourceType, TileModel.Mode, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The templates are not available, nothing is asked to come with {Type}", resourceType);
            return FhirNeeds.None;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, TileModel>> RenderAsync(IReadOnlyList<ResourceSummary> items, string? release = null, IReadOnlyList<RelatedResource>? related = null, string? terminologyServer = null,
                                                                          TileContext? context = null, IViewerServer? server = null, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, TileModel>(StringComparer.Ordinal);
        if (items.Count == 0)
        {
            return result;
        }

        FhirRenderer? renderer = null;
        ModelInspector? model = null;
        try
        {
            renderer = await renderers.GetAsync(cancellationToken);
            model = await models.GetAsync(release, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The templates or the model of {Release} are not available, the rows show the ids", release);
            renderer = null;
        }

        var pool = FhirRenderer.CreatePool(items.Where(item => item.Json is not null).Select(item => new PoolEntry(item.Json!))
                                                .Concat((related ?? []).Select(resource => new PoolEntry(resource.Json, resource.FullUrl))), model);
        await TerminologyPool.AttachAsync(terminology, pool, terminologyServer, items.Where(item => item.Json is not null).Select(item => item.Json!).Concat((related ?? []).Select(resource => resource.Json)), TimeSpan.FromSeconds(1), cancellationToken);
        var missed = await RenderAllAsync(items, renderer, model, pool, context, result);
        if (renderer is not null && server is not null && references is not null && pool.Misses.Count > 0)
        {
            var fetched = await references.FetchAsync(server, [.. pool.Misses], cancellationToken);
            if (fetched.Found.Count > 0 || fetched.Missing.Count > 0)
            {
                pool.AddAll(fetched.Found.Select(resource => new PoolEntry(resource.Json, resource.FullUrl)), model);
                pool.Missing.UnionWith(fetched.Missing);
                // Only the rows that looked for them: the others would come out the same
                await RenderAllAsync(missed, renderer, model, pool, context, result);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> RenderSlotsAsync(IReadOnlyList<ResourceSummary> items, string template, string? release = null,
                                                                                                    IReadOnlyList<RelatedResource>? related = null, string? terminologyServer = null,
                                                                                                    TileContext? context = null, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        if (items.Count == 0)
        {
            return result;
        }

        FhirRenderer renderer;
        ModelInspector model;
        try
        {
            renderer = await renderers.GetAsync(cancellationToken);
            model = await models.GetAsync(release, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The templates or the model of {Release} are not available, the rows of {Template} are not rendered", release, template);
            return result;
        }

        var pool = FhirRenderer.CreatePool(items.Where(item => item.Json is not null).Select(item => new PoolEntry(item.Json!))
                                                .Concat((related ?? []).Select(resource => new PoolEntry(resource.Json, resource.FullUrl))), model);
        await TerminologyPool.AttachAsync(terminology, pool, terminologyServer, items.Where(item => item.Json is not null).Select(item => item.Json!), TimeSpan.FromSeconds(1), cancellationToken);
        var slice = System.Diagnostics.Stopwatch.StartNew();
        foreach (var item in items.Where(item => item.Json is not null))
        {
            if (renderYield is not null && slice.ElapsedMilliseconds > YieldAfterMs)
            {
                await renderYield.YieldAsync();
                slice.Restart();
            }

            try
            {
                result[item.Id] = await renderer.RenderSlotsAsync(item.Json!, template, model, pool, context);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "The row {Template} of {Type} {Id} cannot be rendered", template, item.ResourceType, item.Id);
            }
        }

        return result;
    }

    // Renders the rows into the result; returns the rows that looked for a resource the pool does not have
    private async Task<List<ResourceSummary>> RenderAllAsync(IReadOnlyList<ResourceSummary> items, FhirRenderer? renderer, ModelInspector? model, ResourcePool pool, TileContext? context,
                                                             Dictionary<string, TileModel> result)
    {
        var missed = new List<ResourceSummary>();
        var slice = System.Diagnostics.Stopwatch.StartNew();
        foreach (var item in items)
        {
            if (renderYield is not null && slice.ElapsedMilliseconds > YieldAfterMs)
            {
                await renderYield.YieldAsync();
                slice.Restart();
            }

            var tile = Plain(item);
            var misses = pool.MissCount;
            if (renderer is not null && item.Json is not null)
            {
                try
                {
                    tile = await renderer.RenderTileAsync(item.Json, model: model, pool: pool, context: context);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "The row of {Type} {Id} cannot be rendered", item.ResourceType, item.Id);
                }
            }

            if (pool.MissCount != misses)
            {
                missed.Add(item);
            }

            result[item.Id] = tile;
        }

        return missed;
    }
}
