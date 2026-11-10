namespace FireChart.Viewer.Core;

/// <summary>
/// The renderer of FHIR resources in force and the CSS of the visualization (the <c>fx-*</c> classes) that goes with it.
/// </summary>
/// <remarks>
/// A host gives its own layers over the vendor library (FireChart: the templates of its server). An implementation may reload what it has from time to time;
/// the renderer it returns is safe to use from several renderings at once.
/// </remarks>
/// <example>
/// <code>
/// public sealed class VendorRenderers(IViewerAssets assets) : ITemplateRenderers
/// {
///     private readonly FhirRenderer _renderer = new([new WebVendorTemplates(assets)]);
///
///     public Task&lt;FhirRenderer&gt; GetAsync(CancellationToken cancellationToken = default) =&gt; Task.FromResult(_renderer);
///
///     public async Task&lt;string&gt; GetStyleAsync(CancellationToken cancellationToken = default) =&gt; (await FxStyle.LoadAsync(assets, cancellationToken)).Compose(null);
/// }
/// </code>
/// </example>
public interface ITemplateRenderers
{
    /// <summary>
    /// The renderer in force.
    /// </summary>
    /// <param name="cancellationToken">Cancels the loading of the templates.</param>
    /// <returns>The renderer.</returns>
    Task<FhirRenderer> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The CSS of the visualization in force: the vendor one with the customer's over it.
    /// </summary>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The CSS text.</returns>
    Task<string> GetStyleAsync(CancellationToken cancellationToken = default);
}
