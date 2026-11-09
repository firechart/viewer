namespace FireChart.Viewer.Core.Forms;

/// <summary>
/// Where a resolved form comes from.
/// </summary>
public enum FormSource
{
    /// <summary>The server of the host application (the forms of the organization over its own packages).</summary>
    Server,

    /// <summary>The configuration of the Viewer (<c>searchForms</c>, <c>clinicalSummary.form</c>).</summary>
    Config,

    /// <summary>The vendor library embedded in <c>FireChart.Viewer.Library</c>.</summary>
    Vendor,

    /// <summary>The last resort of the resolver, when no provider has a form for the key.</summary>
    Fallback
}

/// <summary>
/// The usual <see cref="IViewerFormProvider{TKey, TDefinition}.Order"/> of the providers: a lower order is asked first and wins among forms of the
/// same kind (designed or generated).
/// </summary>
/// <example>
/// <code>
/// public int Order => FormProviderOrder.Config;
/// </code>
/// </example>
public static class FormProviderOrder
{
    /// <summary>The server of the host application: 100.</summary>
    public const int Server = 100;

    /// <summary>The configuration of the Viewer: 200.</summary>
    public const int Config = 200;

    /// <summary>The vendor library: 300.</summary>
    public const int Vendor = 300;
}

/// <summary>
/// The key of a search form: the resource type of a FHIR release, with a profile for the forms of an implementation guide.
/// </summary>
/// <param name="Release">The FHIR release in lower case, as the folders of the vendor library (<c>r3</c>, <c>r4</c>, <c>r4b</c>, <c>r5</c>).</param>
/// <param name="ResourceType">The resource type (<c>Patient</c>), case-sensitive as in FHIR.</param>
/// <param name="Profile">The canonical URL or the id of a profile (<c>us-core-patient</c>); not used by the vendor library, reserved for the
/// servers that index implementation guides.</param>
/// <example>
/// <code>
/// var key = new SearchFormKey("r4", "Patient");
/// </code>
/// </example>
public sealed record SearchFormKey(string Release, string ResourceType, string? Profile = null);

/// <summary>
/// The key of a clinical summary form: the code of the form chosen for a FHIR server.
/// </summary>
/// <param name="Code">The <see cref="ClinicalSummaryFormDefinition.Code"/> (<c>default</c>).</param>
/// <example>
/// <code>
/// var key = new SummaryFormKey(server.ClinicalSummaryFormCode ?? ClinicalSummaryFormDefinition.DefaultCode);
/// </code>
/// </example>
public sealed record SummaryFormKey(string Code);

/// <summary>
/// A form a provider has for a key.
/// </summary>
/// <typeparam name="T">The definition of the form.</typeparam>
/// <param name="Definition">The form. The resolver hands it out as it is: give a new instance, or one nobody changes.</param>
/// <param name="IsGenerated">The form was made from search parameters and not designed: any designed form of another provider wins over it.</param>
/// <param name="Version">An opaque version of the form (a hash of its text, an ETag), for caches and logs.</param>
/// <example>
/// <code>
/// return new FormCandidate&lt;SearchFormDefinition&gt;(definition, definition.IsGenerated, TemplateHash.Of(text));
/// </code>
/// </example>
public sealed record FormCandidate<T>(T Definition, bool IsGenerated, string Version);

/// <summary>
/// The form the resolver chose for a key.
/// </summary>
/// <typeparam name="T">The definition of the form.</typeparam>
/// <param name="Definition">The form. It is shared by every caller of the same key until the cache is cleared: do not change it.</param>
/// <param name="Source">The source of the provider that had it, or <see cref="FormSource.Fallback"/>.</param>
/// <param name="IsGenerated">The form was made from search parameters and not designed.</param>
/// <example>
/// <code>
/// var form = await resolver.ResolveAsync(new SearchFormKey("r4", "Patient"), cancellationToken);
/// if (form.Source == FormSource.Fallback)
/// {
///     logger.LogInformation("No form for Patient; the plain one is used");
/// }
/// </code>
/// </example>
public sealed record ResolvedForm<T>(T Definition, FormSource Source, bool IsGenerated);

/// <summary>
/// A source of forms by key: the server of the host application, the configuration of the Viewer, the vendor library.
/// </summary>
/// <typeparam name="TKey">The key: <see cref="SearchFormKey"/> or <see cref="SummaryFormKey"/>.</typeparam>
/// <typeparam name="TDefinition">The form: <see cref="SearchFormDefinition"/> or <see cref="ClinicalSummaryFormDefinition"/>.</typeparam>
/// <remarks>
/// A provider that cannot answer (no connection, <c>401</c>, a timeout) may throw: the resolver logs a warning and asks the next one, so a Viewer with a
/// server is never worse than one without. <see cref="OperationCanceledException"/> of the caller's token is not swallowed.
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyFormProvider : IViewerFormProvider&lt;SearchFormKey, SearchFormDefinition&gt;
/// {
///     public int Order => FormProviderOrder.Config;
///     public FormSource Source => FormSource.Config;
///     public event EventHandler? Changed;
///
///     public Task&lt;FormCandidate&lt;SearchFormDefinition&gt;?&gt; FindAsync(SearchFormKey key, CancellationToken cancellationToken)
///     {
///         return Task.FromResult&lt;FormCandidate&lt;SearchFormDefinition&gt;?&gt;(null);
///     }
/// }
/// </code>
/// </example>
public interface IViewerFormProvider<in TKey, TDefinition>
{
    /// <summary>
    /// The place of the provider: a lower order is asked first (<see cref="FormProviderOrder"/>).
    /// </summary>
    int Order { get; }

    /// <summary>
    /// The source the resolved forms of this provider report.
    /// </summary>
    FormSource Source { get; }

    /// <summary>
    /// Raised when the forms of the provider change (the server has a new version); the resolver forgets what it has resolved.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Finds the form of a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The form, or <see langword="null"/> when the provider has none for the key.</returns>
    Task<FormCandidate<TDefinition>?> FindAsync(TKey key, CancellationToken cancellationToken);
}

/// <summary>
/// Chooses the form of a key among the providers.
/// </summary>
/// <typeparam name="TKey">The key: <see cref="SearchFormKey"/> or <see cref="SummaryFormKey"/>.</typeparam>
/// <typeparam name="TDefinition">The form.</typeparam>
/// <example>
/// <code>
/// IViewerFormResolver&lt;SummaryFormKey, ClinicalSummaryFormDefinition&gt; resolver = ...;
/// var summary = await resolver.ResolveAsync(new SummaryFormKey("default"), cancellationToken);
/// </code>
/// </example>
public interface IViewerFormResolver<in TKey, TDefinition>
{
    /// <summary>
    /// Resolves the form of a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The chosen form; never <see langword="null"/>: without any provider's form it is the fallback.</returns>
    Task<ResolvedForm<TDefinition>> ResolveAsync(TKey key, CancellationToken cancellationToken);
}
