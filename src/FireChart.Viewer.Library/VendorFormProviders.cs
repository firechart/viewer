using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer.Library;

/// <summary>
/// The vendor search forms (<see cref="VendorSearchForms"/>) as a provider of a <see cref="ViewerFormResolver{TKey, TDefinition}"/>: it reads the static
/// file of the form of a key through <see cref="IViewerAssets"/> when the key is first resolved.
/// </summary>
/// <param name="assets">Reads the static files of the host.</param>
/// <param name="logger">Receives what the lax reading of a file left out (<see cref="SearchFormReader"/>); none: nothing is logged.</param>
/// <remarks>
/// A form of a profile (<see cref="SearchFormKey.Profile"/>) is not in the vendor library: the key with a profile gets the form of the resource type.
/// A type without a file has no vendor form. A file is read laxly: an older format is upgraded, what is not understood is left out with a warning;
/// a file that gives no form at all is an exception, so the resolver goes on to the next provider and asks again next time. The files of a release
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
public sealed class VendorSearchFormProvider(IViewerAssets assets, ILogger? logger = null) : IViewerFormProvider<SearchFormKey, SearchFormDefinition>
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

        var read = SearchFormReader.Read(text);
        foreach (var problem in read.Problems)
        {
            logger?.LogWarning("The search form {Url}: {Path} {Message}", url, problem.Path, problem.Message);
        }

        var definition = read.Form ?? throw new FormatException($"The search form {url} gives no form: {string.Join("; ", read.Problems.Select(p => p.Message))}");
        return new FormCandidate<SearchFormDefinition>(definition, definition.IsGenerated, TemplateHash.Of(text));
    }
}

/// <summary>
/// The vendor summary forms (<see cref="VendorSummaryForms"/>) as a provider of a <see cref="ViewerFormResolver{TKey, TDefinition}"/>: it reads
/// the static file of the form of a type through <see cref="IViewerAssets"/> when the type is first resolved.
/// </summary>
/// <param name="assets">Reads the static files of the host.</param>
/// <remarks>
/// A type without a form has no summary: the fallback of the resolver stands for "none" (<see cref="FormSource.Fallback"/>). The files of a release
/// never change, so <see cref="Changed"/> is never raised.
/// </remarks>
/// <example>
/// <code>
/// using var resolver = new ViewerFormResolver&lt;SummaryFormKey, SummaryFormDefinition&gt;(
///     [new VendorSummaryFormProvider(assets)],
///     (key, cancellationToken) =&gt; Task.FromResult(new SummaryFormDefinition()));
/// var form = await resolver.ResolveAsync(new SummaryFormKey("Patient"), CancellationToken.None);
/// </code>
/// </example>
public sealed class VendorSummaryFormProvider(IViewerAssets assets) : IViewerFormProvider<SummaryFormKey, SummaryFormDefinition>
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
    public async Task<FormCandidate<SummaryFormDefinition>?> FindAsync(SummaryFormKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var text = await VendorSummaryForms.TextAsync(assets, key.ResourceType, cancellationToken);
        return text is null ? null : new FormCandidate<SummaryFormDefinition>(ViewerJson.Read<SummaryFormDefinition>(text), false, TemplateHash.Of(text));
    }
}
