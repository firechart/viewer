using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Core.Search;
using FireChart.Viewer.Library;
using Flox.Core.Host;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// <param name="dialogs">The dialogs of Flox.</param>
    /// <param name="definition">The form.</param>
    /// <param name="title">The title of the sheet.</param>
    /// <param name="values">The values the form opens with (<see cref="SearchFormSearchMapping.Read"/>); none: the defaults.</param>
    /// <param name="session">The session of the list: a reference field offers the records opened in it and the list of its type to choose from; none:
    /// a reference field searches as the person types only.</param>
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
    public static async Task<SearchFormDialogResult?> ShowAsync(Flox.Shared.Components.FloxDialogService dialogs, SearchFormDefinition definition, string title, IReadOnlyDictionary<string, SearchFieldValue>? values = null,
        IViewerServer? server = null, ViewerSession? session = null)
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

        if (session is not null)
        {
            parameters[nameof(SearchFormView.Session)] = session;
        }

        // The title of the form above the fields, beside the close button; as high as the fields (the whole screen on a phone); Cancel, Esc and a
        // click beside it close it
        var options = Flox.Shared.Components.SheetOptions.Modal() with { Height = null };
        return await dialogs.OpenAsync<SearchFormView>(title, parameters, options) as SearchFormDialogResult;
    }
}

/// <summary>
/// The search form of a resource type: the component <c>components/{Type}.search.xml</c> of the library (the vendor's, a customer's over it), one file
/// for every FHIR release, over the form generated from the metadata of the release (<see cref="SearchFormGenerator"/>); without the component, or with
/// one that does not pass its check, the generated form. Scoped: the answers are kept for the scope.
/// </summary>
/// <example>
/// <code>
/// var form = await searchForms.FindAsync(new SearchFormKey("r4", "Patient"));
/// var canFilter = form is not null;
/// </code>
/// </example>
[FloxService(ServiceLifetime.Scoped)]
public sealed class ViewerSearchForms
{
    private readonly FireChart.Viewer.Core.Reports.ReportLibrary _library;
    private readonly ILogger<ViewerSearchForms> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<SearchFormKey, Lazy<Task<SearchFormDefinition?>>> _forms = new();

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="assets">Reads the files of the library.</param>
    /// <param name="logger">Logs a search form that does not pass its check.</param>
    public ViewerSearchForms(IViewerAssets assets, ILogger<ViewerSearchForms> logger)
    {
        _library = new FireChart.Viewer.Core.Reports.ReportLibrary(assets, VendorLibrary.UrlOf);
        _logger = logger;
    }

    /// <summary>
    /// The form of a key.
    /// </summary>
    /// <param name="key">The release in lower case and the resource type.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The form of the library, else the generated one; <see langword="null"/> for a release without metadata or a type it does not have (the
    /// list then has no filter).</returns>
    /// <example>
    /// <code>
    /// var form = await searchForms.FindAsync(new SearchFormKey("r4", "Basic"));   // generated: nobody designs a form of Basic
    /// </code>
    /// </example>
    public Task<SearchFormDefinition?> FindAsync(SearchFormKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _forms.GetOrAdd(key, k => new Lazy<Task<SearchFormDefinition?>>(() => ReadAsync(k, cancellationToken))).Value;
    }

    private async Task<SearchFormDefinition?> ReadAsync(SearchFormKey key, CancellationToken cancellationToken)
    {
        var bundle = await _library.LoadAsync(SearchFormComponents.NameOf(key.ResourceType), cancellationToken);
        if (bundle is { Missing: false, Root: { } root, Problems.Length: 0 })
        {
            var culture = System.Globalization.CultureInfo.CurrentUICulture;
            var strings = await _library.StringsAsync(culture, cancellationToken);
            if (SearchFormComponents.Read(root, key.Release, strings, culture) is { } form)
            {
                return form;
            }
        }
        else if (!bundle.Missing)
        {
            _logger.LogWarning("The search form of {Type} is not shown, the generated one is: {Problems}", key.ResourceType, string.Join("; ", bundle.Problems));
        }

        return SearchFormGenerator.Generate(key.Release, key.ResourceType);
    }
}
