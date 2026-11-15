using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Library;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer;

/// <summary>
/// The summary form of a resource type (<c>summary-forms/{Type}.json</c>), chosen by a <see cref="ViewerFormResolver{TKey, TDefinition}"/> among the
/// providers of the host (<see cref="IViewerFormProvider{TKey, TDefinition}"/> services) and the vendor forms of the library; none when no one has
/// it, and then the type has no summary. Scoped: the answers (a missing form too) are kept for the scope.
/// </summary>
/// <example>
/// <code>
/// if (await summaryForms.FindAsync("Encounter") is { } form)
/// {
///     var blocks = form.Sections.Count(section =&gt; section.Visible);
/// }
/// </code>
/// </example>
[FloxService(ServiceLifetime.Scoped)]
public sealed class ViewerSummaryForms : IDisposable
{
    private readonly ViewerFormResolver<SummaryFormKey, SummaryFormDefinition> _resolver;
    private readonly ILogger<ViewerSummaryForms> _logger;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="providers">The providers of the host.</param>
    /// <param name="assets">Reads the vendor forms, the static files of the vendor library.</param>
    /// <param name="logger">Logs a provider that failed and a form that does not grow from its type.</param>
    public ViewerSummaryForms(IEnumerable<IViewerFormProvider<SummaryFormKey, SummaryFormDefinition>> providers, IViewerAssets assets, ILogger<ViewerSummaryForms> logger)
    {
        _logger = logger;
        _resolver = new ViewerFormResolver<SummaryFormKey, SummaryFormDefinition>([.. providers, new VendorSummaryFormProvider(assets)],
            (_, _) => Task.FromResult(new SummaryFormDefinition()), logger);
    }

    /// <summary>
    /// The form of a resource type.
    /// </summary>
    /// <param name="resourceType">The type of the record the summary grows from (<c>Patient</c>).</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The form; <see langword="null"/> when no provider has one, or the one found grows from another type (its <see cref="SummaryFormDefinition.Context"/>).</returns>
    /// <example>
    /// <code>
    /// var form = await summaryForms.FindAsync("Patient");
    /// </code>
    /// </example>
    public async Task<SummaryFormDefinition?> FindAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceType);
        if (VendorSummaryForms.PathOf(resourceType) is null)
        {
            return null;
        }

        var resolved = await _resolver.ResolveAsync(new SummaryFormKey(resourceType), cancellationToken);
        if (resolved.Source == FormSource.Fallback)
        {
            return null;
        }

        if (resolved.Definition.Context != resourceType)
        {
            _logger.LogWarning("The summary form of {Type} grows from {Context}; the type has no summary", resourceType, resolved.Definition.Context);
            return null;
        }

        return resolved.Definition;
    }

    /// <summary>
    /// Whether a resource type has a summary: the switch between the record and its summary shows only then.
    /// </summary>
    /// <param name="resourceType">The type of the record.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns><see langword="true"/> when <see cref="FindAsync"/> finds a form.</returns>
    /// <example>
    /// <code>
    /// var summary = await summaryForms.HasFormAsync(route.ResourceType);
    /// </code>
    /// </example>
    public async Task<bool> HasFormAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        return await FindAsync(resourceType, cancellationToken) is not null;
    }

    /// <summary>
    /// Unsubscribes from the providers.
    /// </summary>
    public void Dispose()
    {
        _resolver.Dispose();
    }
}
