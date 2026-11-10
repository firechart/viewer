using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Library;

/// <summary>
/// The vendor search forms (<see cref="VendorSearchForms"/>) as a provider of a <see cref="ViewerFormResolver{TKey, TDefinition}"/>: it reads the static
/// file of the form of a key through <see cref="IViewerAssets"/> when the key is first resolved.
/// </summary>
/// <param name="assets">Reads the static files of the host.</param>
/// <remarks>
/// A form of a profile (<see cref="SearchFormKey.Profile"/>) is not in the vendor library: the key with a profile gets the form of the resource type.
/// A type without a file has no vendor form; a file that cannot be read is an exception, so the resolver asks again next time. The files of a release
/// never change (a new release has a new address), so <see cref="Changed"/> is never raised.
/// </remarks>
/// <example>
/// <code>
/// using var resolver = new ViewerFormResolver&lt;SearchFormKey, SearchFormDefinition&gt;(
///     [new VendorSearchFormProvider(assets)],
///     (key, cancellationToken) =&gt; Task.FromResult(new SearchFormDefinition { Name = "plain" }));
/// var patient = await resolver.ResolveAsync(new SearchFormKey("r4", "Patient"), CancellationToken.None);
/// </code>
/// </example>
public sealed class VendorSearchFormProvider(IViewerAssets assets) : IViewerFormProvider<SearchFormKey, SearchFormDefinition>
{
    /// <inheritdoc/>
    public int Order => FormProviderOrder.Vendor;

    /// <inheritdoc/>
    public FormSource Source => FormSource.Vendor;

    /// <inheritdoc/>
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public async Task<FormCandidate<SearchFormDefinition>?> FindAsync(SearchFormKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var url = VendorSearchForms.UrlOf(key.Release, key.ResourceType);
        var text = url is null ? null : await assets.ReadTextAsync(url, cancellationToken);
        if (text is null)
        {
            return null;
        }

        var definition = ViewerJson.Read<SearchFormDefinition>(text);
        return new FormCandidate<SearchFormDefinition>(definition, definition.IsGenerated, TemplateHash.Of(text));
    }
}

/// <summary>
/// The vendor clinical summary forms (<see cref="VendorSummaryForms"/>) as a provider of a <see cref="ViewerFormResolver{TKey, TDefinition}"/>: it reads
/// the static file of the form of a code through <see cref="IViewerAssets"/> when the code is first resolved.
/// </summary>
/// <param name="assets">Reads the static files of the host.</param>
/// <remarks>
/// The only fallback of the summary forms is the vendor form <c>default</c> (<see cref="VendorSummaryForms.FindAsync"/>): give it to the resolver as its
/// fallback. The files of a release never change, so <see cref="Changed"/> is never raised.
/// </remarks>
/// <example>
/// <code>
/// using var resolver = new ViewerFormResolver&lt;SummaryFormKey, ClinicalSummaryFormDefinition&gt;(
///     [new VendorSummaryFormProvider(assets)],
///     async (key, cancellationToken) =&gt; (await VendorSummaryForms.FindAsync(assets, "default", cancellationToken))!);
/// var form = await resolver.ResolveAsync(new SummaryFormKey("default"), CancellationToken.None);
/// </code>
/// </example>
public sealed class VendorSummaryFormProvider(IViewerAssets assets) : IViewerFormProvider<SummaryFormKey, ClinicalSummaryFormDefinition>
{
    /// <inheritdoc/>
    public int Order => FormProviderOrder.Vendor;

    /// <inheritdoc/>
    public FormSource Source => FormSource.Vendor;

    /// <inheritdoc/>
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public async Task<FormCandidate<ClinicalSummaryFormDefinition>?> FindAsync(SummaryFormKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var text = await VendorSummaryForms.TextAsync(assets, key.Code, cancellationToken);
        return text is null ? null : new FormCandidate<ClinicalSummaryFormDefinition>(ViewerJson.Read<ClinicalSummaryFormDefinition>(text), false, TemplateHash.Of(text));
    }
}
