using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Library;
using Flox.Core.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FireChart.Viewer;

/// <summary>
/// The clinical summary form of a code, chosen by a <see cref="ViewerFormResolver{TKey, TDefinition}"/> among the providers of the host
/// (<see cref="IViewerFormProvider{TKey, TDefinition}"/> services) and the vendor forms of the library; the vendor <c>default</c> when no one has it.
/// Scoped: the answers are kept for the scope.
/// </summary>
/// <example>
/// <code>
/// var form = await summaryForms.GetAsync(session.ClinicalSummaryForm);
/// var blocks = form.Sections.Count(section =&gt; section.Visible);
/// </code>
/// </example>
[FloxService(ServiceLifetime.Scoped)]
public sealed class ViewerSummaryForms : IDisposable
{
    private readonly ViewerFormResolver<SummaryFormKey, ClinicalSummaryFormDefinition> _resolver;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="providers">The providers of the host.</param>
    /// <param name="logger">Logs a provider that failed.</param>
    public ViewerSummaryForms(IEnumerable<IViewerFormProvider<SummaryFormKey, ClinicalSummaryFormDefinition>> providers, ILogger<ViewerSummaryForms> logger)
    {
        _resolver = new ViewerFormResolver<SummaryFormKey, ClinicalSummaryFormDefinition>([.. providers, new VendorSummaryFormProvider()], (_, _) => Task.FromResult(VendorSummaryForms.Default), logger);
    }

    /// <summary>
    /// The form of a code.
    /// </summary>
    /// <param name="code">The code of the form chosen for the server; empty is <c>default</c>.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The form; the vendor <c>default</c> when no provider has the code.</returns>
    public async Task<ClinicalSummaryFormDefinition> GetAsync(string? code, CancellationToken cancellationToken = default)
    {
        var key = new SummaryFormKey(string.IsNullOrWhiteSpace(code) ? ClinicalSummaryFormDefinition.DefaultCode : code.Trim());
        return (await _resolver.ResolveAsync(key, cancellationToken)).Definition;
    }

    /// <summary>
    /// Unsubscribes from the providers.
    /// </summary>
    public void Dispose()
    {
        _resolver.Dispose();
    }
}
