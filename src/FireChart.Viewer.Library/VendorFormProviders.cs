using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Library;

/// <summary>
/// The vendor search forms (<see cref="VendorSearchForms"/>) as a provider of a <see cref="ViewerFormResolver{TKey, TDefinition}"/>.
/// </summary>
/// <remarks>
/// A form of a profile (<see cref="SearchFormKey.Profile"/>) is not in the vendor library: the key with a profile gets the form of the resource type.
/// The forms never change while the assembly is loaded, so <see cref="Changed"/> is never raised.
/// </remarks>
/// <example>
/// <code>
/// using var resolver = new ViewerFormResolver&lt;SearchFormKey, SearchFormDefinition&gt;(
///     [new VendorSearchFormProvider()],
///     (key, cancellationToken) =&gt; Task.FromResult(new SearchFormDefinition { Name = "plain" }));
/// var patient = await resolver.ResolveAsync(new SearchFormKey("r4", "Patient"), CancellationToken.None);
/// </code>
/// </example>
public sealed class VendorSearchFormProvider : IViewerFormProvider<SearchFormKey, SearchFormDefinition>
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
    public Task<FormCandidate<SearchFormDefinition>?> FindAsync(SearchFormKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var text = VendorSearchForms.TextOf(key.Release, key.ResourceType);
        if (text is null)
        {
            return Task.FromResult<FormCandidate<SearchFormDefinition>?>(null);
        }

        var definition = ViewerJson.Read<SearchFormDefinition>(text);
        return Task.FromResult<FormCandidate<SearchFormDefinition>?>(new FormCandidate<SearchFormDefinition>(definition, definition.IsGenerated, TemplateHash.Of(text)));
    }
}

/// <summary>
/// The vendor clinical summary forms (<see cref="VendorSummaryForms"/>) as a provider of a <see cref="ViewerFormResolver{TKey, TDefinition}"/>.
/// </summary>
/// <remarks>
/// The only fallback of the summary forms is the vendor form <c>default</c> (<see cref="VendorSummaryForms.Default"/>): give it to the resolver as its
/// fallback. The forms never change while the assembly is loaded, so <see cref="Changed"/> is never raised.
/// </remarks>
/// <example>
/// <code>
/// using var resolver = new ViewerFormResolver&lt;SummaryFormKey, ClinicalSummaryFormDefinition&gt;(
///     [new VendorSummaryFormProvider()],
///     (key, cancellationToken) =&gt; Task.FromResult(VendorSummaryForms.Default));
/// var form = await resolver.ResolveAsync(new SummaryFormKey("default"), CancellationToken.None);
/// </code>
/// </example>
public sealed class VendorSummaryFormProvider : IViewerFormProvider<SummaryFormKey, ClinicalSummaryFormDefinition>
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
    public Task<FormCandidate<ClinicalSummaryFormDefinition>?> FindAsync(SummaryFormKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var text = VendorSummaryForms.TextOf(key.Code);
        return Task.FromResult(text is null ? null : new FormCandidate<ClinicalSummaryFormDefinition>(ViewerJson.Read<ClinicalSummaryFormDefinition>(text), false, TemplateHash.Of(text)));
    }
}
