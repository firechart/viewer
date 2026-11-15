using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Core.Search;
using FireChart.Viewer.Library;
using Flox.Core.Host;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;

namespace FireChart.Viewer.SearchForms;

/// <summary>
/// The values a select field of a search form offers: the expansion of the value set of the field. The host supplies it (FireChart: the value sets of
/// its packages); without one a field offers the options of the form only.
/// </summary>
/// <example>
/// <code>
/// [FloxService(typeof(ISearchFormOptionSource), ServiceLifetime.Scoped)]
/// public sealed class MyOptions : ISearchFormOptionSource
/// {
///     public Task&lt;IReadOnlyList&lt;SearchOption&gt;&gt; GetOptionsAsync(string valueSetUrl, string? version, CancellationToken cancellationToken = default)
///         =&gt; Task.FromResult&lt;IReadOnlyList&lt;SearchOption&gt;&gt;([new SearchOption("female", "Female")]);
/// }
/// </code>
/// </example>
public interface ISearchFormOptionSource
{
    /// <summary>
    /// The options of a value set.
    /// </summary>
    /// <param name="valueSetUrl">The canonical URL of the value set.</param>
    /// <param name="version">Its version, or <see langword="null"/> for any.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The options; empty when the value set is not known.</returns>
    Task<IReadOnlyList<SearchOption>> GetOptionsAsync(string valueSetUrl, string? version, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the search dialog gives back.
/// </summary>
/// <param name="Conditions">The conditions of the form now.</param>
/// <param name="Values">The values of its fields, by query name: give them back to open the form with them again.</param>
/// <example>
/// <code>
/// var next = SearchFormSearchMapping.Apply(search, read, result.Conditions);
/// </code>
/// </example>
public sealed record SearchFormDialogResult(IReadOnlyList<SearchCondition> Conditions, IReadOnlyDictionary<string, SearchFieldValue> Values);

/// <summary>
/// Opens a search form as a modal sheet.
/// </summary>
public static class SearchFormDialog
{
    /// <summary>
    /// Shows a form and waits for the user.
    /// </summary>
    /// <param name="dialogs">The dialogs of Radzen.</param>
    /// <param name="definition">The form.</param>
    /// <param name="title">The title of the sheet.</param>
    /// <param name="values">The values the form opens with (<see cref="SearchFormSearchMapping.Read"/>); none: the defaults.</param>
    /// <param name="server">
    /// The FHIR server the search goes to: a field bound to a value set offers the codes this server expands (<see cref="IValueSetExpander"/>); none, or a
    /// server that does not expand it: the codes of the <see cref="ISearchFormOptionSource"/> of the host, else a text box.
    /// </param>
    /// <returns>The conditions and the values, or <see langword="null"/> when the user cancelled.</returns>
    /// <example>
    /// <code>
    /// var read = SearchFormSearchMapping.Read(definition, search);
    /// var result = await SearchFormDialog.ShowAsync(dialogs, definition, "Patients", read.Values, server);
    /// </code>
    /// </example>
    public static async Task<SearchFormDialogResult?> ShowAsync(DialogService dialogs, SearchFormDefinition definition, string title, IReadOnlyDictionary<string, SearchFieldValue>? values = null,
        IViewerServer? server = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(definition);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(SearchFormView.Definition)] = definition
        };
        if (values is not null)
        {
            parameters[nameof(SearchFormView.Values)] = values;
        }

        if (server is not null)
        {
            parameters[nameof(SearchFormView.Server)] = server;
        }

        // No title bar: the form takes the height; Cancel, Esc and a click beside it close it
        var options = Flox.Shared.Components.SheetOptions.Modal();
        options.ShowTitle = false;
        return await dialogs.OpenAsync<SearchFormView>(title, parameters, options) as SearchFormDialogResult;
    }
}

/// <summary>
/// The search form of a resource type, chosen by a <see cref="ViewerFormResolver{TKey, TDefinition}"/> among the providers of the host
/// (<see cref="IViewerFormProvider{TKey, TDefinition}"/> services) and the designed forms of the library (<c>search-forms/{release}/{type}.json</c>);
/// when none has one, the form is generated from the metadata of the release (<see cref="SearchFormGenerator"/>). Scoped: the answers are kept for
/// the scope.
/// </summary>
/// <example>
/// <code>
/// var form = await searchForms.FindAsync(new SearchFormKey("r4", "Patient"));
/// var canFilter = form is not null;
/// </code>
/// </example>
[FloxService(ServiceLifetime.Scoped)]
public sealed class ViewerSearchForms : IDisposable
{
    private readonly ViewerFormResolver<SearchFormKey, SearchFormDefinition> _resolver;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="providers">The providers of the host.</param>
    /// <param name="assets">Reads the vendor forms, the static files of the vendor library.</param>
    /// <param name="logger">Logs a provider that failed.</param>
    public ViewerSearchForms(IEnumerable<IViewerFormProvider<SearchFormKey, SearchFormDefinition>> providers, IViewerAssets assets, ILogger<ViewerSearchForms> logger)
    {
        _resolver = new ViewerFormResolver<SearchFormKey, SearchFormDefinition>([.. providers, new VendorSearchFormProvider(assets, logger)],
                                                                                (key, _) => Task.FromResult(SearchFormGenerator.Generate(key.Release, key.ResourceType) ?? new SearchFormDefinition()),
                                                                                logger);
    }

    /// <summary>
    /// The form of a key.
    /// </summary>
    /// <param name="key">The release in lower case and the resource type.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The form of a provider, else the generated one; <see langword="null"/> for a release without metadata or a type it does not have (the
    /// list then has no filter).</returns>
    /// <example>
    /// <code>
    /// var form = await searchForms.FindAsync(new SearchFormKey("r4", "Basic"));   // generated: nobody designs a form of Basic
    /// </code>
    /// </example>
    public async Task<SearchFormDefinition?> FindAsync(SearchFormKey key, CancellationToken cancellationToken = default)
    {
        var resolved = await _resolver.ResolveAsync(key, cancellationToken);
        return resolved.Source == FormSource.Fallback && resolved.Definition.Fields.Count == 0 ? null : resolved.Definition;
    }

    /// <summary>
    /// Unsubscribes from the providers.
    /// </summary>
    public void Dispose()
    {
        _resolver.Dispose();
    }
}
