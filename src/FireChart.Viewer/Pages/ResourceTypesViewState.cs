using Flox.Shared.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FireChart.Viewer.Pages;

/// <summary>
/// The state of the resource types of a server: the types its <c>CapabilityStatement</c> declares, the most used first, and the access to the server.
/// Activated with the <see cref="ViewerSession"/>.
/// </summary>
/// <example>
/// <code>
/// await state.ActivateAsync(session);
/// var first = state.Visible.FirstOrDefault();
/// </code>
/// </example>
public class ResourceTypesViewState : State
{
    /// <summary>A row of the list that is not a type: the clinical summary of a patient (before Patient, when the server has it).</summary>
    public const string ClinicalSummaryEntry = "#summary";

    /// <summary>The entry of the bookmarks in <see cref="Visible"/>, first, when the host keeps bookmarks.</summary>
    public const string BookmarksEntry = "#bookmarks";

    // The ones that clinicians open most, they come first
    private static readonly string[] s_frequent = ["Patient", "Encounter", "Observation", "Condition", "Procedure"];

    private readonly IServerCredentialStore _credentials;
    private readonly IServerResourceTypeProvider _provider;
    private ViewerSession? _session;
    private IReadOnlyList<string> _types = [];

    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope.</param>
    public ResourceTypesViewState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _credentials = serviceProvider.GetRequiredService<IServerCredentialStore>();
        _provider = serviceProvider.GetRequiredService<IServerResourceTypeProvider>();
    }

    /// <summary>The server, once the state is activated.</summary>
    public IViewerServer? Server { get; private set; }

    /// <summary>The release the server declares (<c>R4</c>); empty until it is read.</summary>
    public string Release { get; private set; } = string.Empty;

    /// <summary>The last connection did not give the types.</summary>
    public bool Failed { get; private set; }

    /// <summary>The types after the filter, with <see cref="ClinicalSummaryEntry"/> first when it fits; a new list on every change.</summary>
    public IReadOnlyList<string> Visible { get; private set; } = [];

    /// <summary>The text the types are filtered by.</summary>
    public string? Filter { get; private set; }

    /// <summary>Whether the server can be read now.</summary>
    public ServerAccess Access => Server is null ? ServerAccess.Ready : _credentials.AccessOf(Server);

    /// <summary>The server did not accept the credentials that were given: the host asks again.</summary>
    public bool CredentialsRejected { get; private set; }

    /// <summary>
    /// Filters the types.
    /// </summary>
    /// <param name="text">The text; empty shows all.</param>
    public void SetFilter(string? text)
    {
        Filter = text;
        Apply();
        NotifyChanged();
    }

    /// <summary>
    /// Reads the server again.
    /// </summary>
    /// <returns>A task that completes when it is read.</returns>
    public async Task RetryAsync()
    {
        if (_session is not null)
        {
            await LoadAsync(_session);
        }
    }

    /// <inheritdoc/>
    protected override async Task OnStartAsync(object? key)
    {
        await LoadAsync((ViewerSession)key!);
    }

    // One request when the server is opened; the answer is kept in memory only
    private async Task LoadAsync(ViewerSession session)
    {
        _session = session;
        await RunAsync(async () =>
        {
            Server = session.Server;
            Failed = false;
            CredentialsRejected = false;
            Release = string.Empty;
            _types = [];
            Filter = null;
            Apply();
            await ServiceProvider.GetRequiredService<ISmartAuthorizer>().RestoreAsync(session.Server);

            if (Access != ServerAccess.Ready)
            {
                return;
            }

            try
            {
                var result = await _provider.GetAsync(session.Server, refresh: true);
                Release = result.Release;
                _types = Sort(result.Types);
                Apply();
            }
            catch (Exception ex) when (ex.IsRejected())
            {
                await ServiceProvider.GetRequiredService<ISmartAuthorizer>().DisconnectAsync(session.Server);

                _credentials.Forget(session.Server);
                CredentialsRejected = true;
            }
            catch
            {
                Failed = true;
                throw;
            }
        });
        NotifyChanged();
    }

    private static string[] Sort(IEnumerable<string> types)
    {
        var all = types.ToList();
        return
        [
            .. s_frequent.Where(all.Contains),
            .. all.Where(row => !s_frequent.Contains(row)).Order(StringComparer.Ordinal)
        ];
    }

    private void Apply()
    {
        IReadOnlyList<string> types = string.IsNullOrWhiteSpace(Filter)
            ? _types
            : [.. _types.Where(row => row.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase))];
        var summary = _types.Contains("Patient") && (string.IsNullOrWhiteSpace(Filter) || Strings.Caption_ClinicalSummary.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase));
        var bookmarks = ViewerBookmarks.AreAvailable(ServiceProvider) && (string.IsNullOrWhiteSpace(Filter) || FloxStrings.Caption_Bookmarks.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase));
        string[] entry = [.. bookmarks ? [BookmarksEntry] : Array.Empty<string>(), .. summary ? [ClinicalSummaryEntry] : Array.Empty<string>()];
        Visible = [.. entry, .. types];
    }
}
