using Flox.Shared.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

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
    /// <summary>The entry of the bookmarks in <see cref="Visible"/>, first, when the host keeps bookmarks.</summary>
    public const string BookmarksEntry = "#bookmarks";

    // The ones that clinicians open most, they come first
    private static readonly string[] s_frequent = ["Patient", "Encounter", "Observation", "Condition", "Procedure"];

    private readonly IServerCredentialStore _credentials;
    private readonly IServerResourceTypeProvider _provider;
    private readonly IViewerStyle _style;
    private ViewerSession? _session;
    private IReadOnlyList<string> _types = [];
    private HashSet<string> _hidden = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the state.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope.</param>
    public ResourceTypesViewState(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _credentials = serviceProvider.GetRequiredService<IServerCredentialStore>();
        _provider = serviceProvider.GetRequiredService<IServerResourceTypeProvider>();
        _style = serviceProvider.GetRequiredService<IViewerStyle>();
    }

    /// <summary>The server, once the state is activated.</summary>
    public IViewerServer? Server { get; private set; }

    /// <summary>The release the server declares (<c>R4</c>); empty until it is read.</summary>
    public string Release { get; private set; } = string.Empty;

    /// <summary>The style of the templates: the colours of the types (<see cref="Components.ResourceTypeColors"/>) are its tokens; empty until it is read.</summary>
    public string Style { get; private set; } = string.Empty;

    /// <summary>The last connection did not give the types.</summary>
    public bool Failed { get; private set; }

    /// <summary>The types after the filter, with <see cref="BookmarksEntry"/> first when it fits; a new list on every change.</summary>
    public IReadOnlyList<string> Visible { get; private set; } = [];

    /// <summary>The text the types are filtered by.</summary>
    public string? Filter { get; private set; }

    /// <summary>Whether the server can be read now.</summary>
    public ServerAccess Access => Server is null ? ServerAccess.Ready : _credentials.AccessOf(Server);

    /// <summary>The server did not accept the credentials that were given: the host asks again.</summary>
    public bool CredentialsRejected { get; private set; }

    /// <summary>The host keeps the choice of the types on the device (<see cref="ResourceTypeVisibility"/>): the page offers to choose them.</summary>
    public bool CanConfigure => ViewerTypeVisibility.Of(ServiceProvider) is not null;

    /// <summary>The page is in its mode of choosing the types: every type is listed, a hidden one marked; the choice is saved when the mode ends.</summary>
    public bool Configuring { get; private set; }

    /// <summary>
    /// Whether a type is hidden on the page.
    /// </summary>
    /// <param name="type">The resource type.</param>
    /// <returns><see langword="true"/> when the type is not shown outside <see cref="Configuring"/>.</returns>
    public bool IsHidden(string type)
    {
        return _hidden.Contains(type);
    }

    /// <summary>
    /// Starts or ends the choosing of the types; at its end the choice is saved on the device.
    /// </summary>
    /// <returns>A task that completes when the choice is saved.</returns>
    public async Task ToggleConfiguringAsync()
    {
        if (Configuring)
        {
            await EndConfiguringAsync();
            return;
        }

        Configuring = CanConfigure;
        Apply();
        NotifyChanged();
    }

    /// <summary>
    /// Ends the choosing of the types and saves the choice; nothing when it is not on.
    /// </summary>
    /// <returns>A task that completes when the choice is saved.</returns>
    public async Task EndConfiguringAsync()
    {
        if (!Configuring)
        {
            return;
        }

        Configuring = false;
        Apply();
        NotifyChanged();
        if (Server is not null && ViewerTypeVisibility.Of(ServiceProvider) is { } visibility)
        {
            await visibility.SaveAsync(Server, _types, _hidden);
        }
    }

    /// <summary>
    /// Hides a type or shows it again, while the types are chosen.
    /// </summary>
    /// <param name="type">The resource type.</param>
    public void ToggleHidden(string type)
    {
        if (!Configuring || type == BookmarksEntry)
        {
            return;
        }

        if (!_hidden.Remove(type))
        {
            _hidden.Add(type);
        }

        Apply();
        NotifyChanged();
    }

    /// <summary>
    /// Shows or hides every type the filter leaves, while the types are chosen.
    /// </summary>
    /// <param name="hidden"><see langword="true"/> hides them, <see langword="false"/> shows them.</param>
    public void SetAllHidden(bool hidden)
    {
        if (!Configuring)
        {
            return;
        }

        foreach (var type in Filtered())
        {
            if (hidden)
            {
                _hidden.Add(type);
            }
            else
            {
                _hidden.Remove(type);
            }
        }

        Apply();
        NotifyChanged();
    }

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
            _hidden = new HashSet<string>(StringComparer.Ordinal);
            Configuring = false;
            Filter = null;
            Apply();
            if (Style.Length == 0)
            {
                Style = await _style.GetStyleAsync();
                await KitStyleSheet.AdoptAsync(ServiceProvider.GetRequiredService<IJSRuntime>(), Style);
            }

            await ServiceProvider.GetRequiredService<ISmartAuthorizer>().RestoreAsync(session.Server);

            if (Access != ServerAccess.Ready)
            {
                return;
            }

            try
            {
                var result = await _provider.GetAsync(session.Server, refresh: true);
                if (ViewerTypeVisibility.Of(ServiceProvider) is { } visibility)
                {
                    _hidden = await visibility.HiddenAsync(session.Server);
                }

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

    private IEnumerable<string> Filtered()
    {
        return string.IsNullOrWhiteSpace(Filter) ? _types : _types.Where(row => row.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    // A hidden type is listed only while the types are chosen
    private void Apply()
    {
        IReadOnlyList<string> types = [.. Filtered().Where(type => Configuring || !_hidden.Contains(type))];
        var bookmarks = ViewerBookmarks.AreAvailable(ServiceProvider) && (string.IsNullOrWhiteSpace(Filter) || FloxStrings.Caption_Bookmarks.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase));
        Visible = [.. bookmarks ? [BookmarksEntry] : Array.Empty<string>(), .. types];
    }
}
