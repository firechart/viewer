using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// The resolver of the forms of one kind over a chain of providers (<c>.tasks/viewer/viewer.md</c>, 4.13).
/// </summary>
/// <typeparam name="TKey">The key: <see cref="SearchFormKey"/> or <see cref="SummaryFormKey"/>; a record, compared by value.</typeparam>
/// <typeparam name="TDefinition">The form.</typeparam>
/// <remarks>
/// <para>The rule:</para>
/// <list type="number">
/// <item><description>a designed form wins over a generated one, whatever the providers (a server that generates forms from its packages does not
/// hide the designed forms of the vendor);</description></item>
/// <item><description>among forms of the same kind the provider of the lower <see cref="IViewerFormProvider{TKey, TDefinition}.Order"/> wins;</description></item>
/// <item><description>a provider that throws is skipped with a warning in the log;</description></item>
/// <item><description>without any form the fallback makes one (<see cref="FormSource.Fallback"/>).</description></item>
/// </list>
/// <para>A resolved form is kept per key until a provider raises <see cref="IViewerFormProvider{TKey, TDefinition}.Changed"/> or
/// <see cref="Clear"/> is called. A fallback form is kept as well; a form chosen while a provider failed is not, so the provider is asked again
/// next time. Dispose the resolver to unsubscribe from the providers.</para>
/// </remarks>
/// <example>
/// <code>
/// using var resolver = new ViewerFormResolver&lt;SummaryFormKey, ClinicalSummaryFormDefinition&gt;(
///     [serverProvider, new VendorSummaryFormProvider()],
///     (key, cancellationToken) =&gt; Task.FromResult(VendorSummaryForms.Default),
///     logger);
/// var form = await resolver.ResolveAsync(new SummaryFormKey("oncology"), cancellationToken);
/// </code>
/// </example>
public sealed class ViewerFormResolver<TKey, TDefinition> : IViewerFormResolver<TKey, TDefinition>, IDisposable
    where TKey : notnull
{
    private readonly IReadOnlyList<IViewerFormProvider<TKey, TDefinition>> _providers;
    private readonly Func<TKey, CancellationToken, Task<TDefinition>> _fallback;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<TKey, ResolvedForm<TDefinition>> _resolved = new();

    /// <summary>
    /// Creates a resolver.
    /// </summary>
    /// <param name="providers">The providers, in any order: they are asked by their <see cref="IViewerFormProvider{TKey, TDefinition}.Order"/>.</param>
    /// <param name="fallback">Makes the form of a key no provider has (a plain search form from the <c>CapabilityStatement</c>, the vendor
    /// <c>default</c> summary form).</param>
    /// <param name="logger">Where the failures of the providers are logged; none by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="providers"/> or <paramref name="fallback"/> is <see langword="null"/>.</exception>
    public ViewerFormResolver(IEnumerable<IViewerFormProvider<TKey, TDefinition>> providers, Func<TKey, CancellationToken, Task<TDefinition>> fallback, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(fallback);
        _providers = [.. providers.OrderBy(provider => provider.Order)];
        _fallback = fallback;
        _logger = logger ?? NullLogger.Instance;
        foreach (var provider in _providers)
        {
            provider.Changed += OnChanged;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<ResolvedForm<TDefinition>> ResolveAsync(TKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_resolved.TryGetValue(key, out var known))
        {
            return known;
        }

        var choice = await ChooseAsync(key, cancellationToken);
        if (choice.Complete)
        {
            _resolved[key] = choice.Form;
        }

        return choice.Form;
    }

    /// <summary>
    /// Forgets every resolved form: the next resolution asks the providers again.
    /// </summary>
    public void Clear()
    {
        _resolved.Clear();
    }

    /// <summary>
    /// Unsubscribes from the <see cref="IViewerFormProvider{TKey, TDefinition}.Changed"/> events of the providers.
    /// </summary>
    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Changed -= OnChanged;
        }
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        Clear();
    }

    // The first designed form in the order wins at once; else the first generated one; else the fallback. Not complete: a provider failed
    private async Task<Choice> ChooseAsync(TKey key, CancellationToken cancellationToken)
    {
        ResolvedForm<TDefinition>? generated = null;
        var complete = true;
        foreach (var provider in _providers)
        {
            FormCandidate<TDefinition>? candidate;
            try
            {
                candidate = await provider.FindAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "The form provider {Provider} failed for {Key}; the next one is asked", provider.GetType().Name, key);
                complete = false;
                continue;
            }

            if (candidate is null)
            {
                continue;
            }

            if (!candidate.IsGenerated)
            {
                return new Choice(new ResolvedForm<TDefinition>(candidate.Definition, provider.Source, false), complete);
            }

            generated ??= new ResolvedForm<TDefinition>(candidate.Definition, provider.Source, true);
        }

        return new Choice(generated ?? new ResolvedForm<TDefinition>(await _fallback(key, cancellationToken), FormSource.Fallback, false), complete);
    }

    private sealed record Choice(ResolvedForm<TDefinition> Form, bool Complete);
}
