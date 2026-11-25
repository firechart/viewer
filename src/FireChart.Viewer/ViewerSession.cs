using FireChart.Viewer.Routing;

namespace FireChart.Viewer;

/// <summary>
/// One instance of the Viewer: its server, its address and the options of its host. The pages of the Viewer get it as a cascading value.
/// </summary>
/// <example>
/// <code>
/// [CascadingParameter]
/// public ViewerSession Session { get; set; } = null!;
///
/// private void OpenPatients() =&gt; Session.Navigate(new ViewerRoute(ViewerPage.ResourceList, "Patient"));
/// </code>
/// </example>
public sealed class ViewerSession
{
    /// <summary>
    /// Creates a session.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="location">The address of the Viewer.</param>
    /// <param name="smart">How a SMART sign-in leaves the page and where it comes back; none: <see cref="FireChart.Viewer.Smart.SmartOptions.Default"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="server"/> or <paramref name="location"/> is <see langword="null"/>.</exception>
    public ViewerSession(IViewerServer server, IViewerLocationStore location, FireChart.Viewer.Smart.SmartOptions? smart = null)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(location);
        Server = server;
        Location = location;
        Smart = smart ?? FireChart.Viewer.Smart.SmartOptions.Default;
        Remember();
    }

    /// <summary>
    /// The server of the instance.
    /// </summary>
    public IViewerServer Server { get; }

    /// <summary>
    /// The address of the instance.
    /// </summary>
    public IViewerLocationStore Location { get; }

    /// <summary>
    /// How a SMART sign-in leaves the page and where it comes back.
    /// </summary>
    public FireChart.Viewer.Smart.SmartOptions Smart { get; }

    /// <summary>
    /// The base address of the terminology server of <see cref="Fhir"/> (<see cref="TerminologyResolver.AddressOf"/>), or <see langword="null"/>: the
    /// codes are shown as they are, nothing of a patient leaves for another server.
    /// </summary>
    public string? TerminologyServer => TerminologyResolver.AddressOf(Fhir.TerminologyServerUrl);

    /// <summary>
    /// The pages show the star of a bookmark (when the host keeps bookmarks); <see langword="true"/> by default. A host whose own pages bookmark
    /// otherwise (the lists of the Conformance Browser of FireChart) turns it off.
    /// </summary>
    /// <example>
    /// <code>
    /// var session = new ViewerSession(server, location) { Bookmarks = false };
    /// </code>
    /// </example>
    public bool Bookmarks { get; init; } = true;

    /// <summary>
    /// The FHIR settings of the instance: the terminology server, how the summary and a report load their records, the decimal places of a value.
    /// </summary>
    /// <example>
    /// <code>
    /// var session = new ViewerSession(server, location) { Fhir = new FhirOptions { LoadAll = false, Decimals = 1 } };
    /// </code>
    /// </example>
    public FhirOptions Fhir { get; init; } = FhirOptions.Default;

    /// <summary>
    /// A page of the Viewer as an <c>href</c> of the document.
    /// </summary>
    /// <param name="route">The page.</param>
    /// <returns>The <c>href</c>, relative to the base of the document.</returns>
    public string HrefOf(ViewerRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return Location.HrefOf(route.Location);
    }

    /// <summary>
    /// Goes to a page of the Viewer.
    /// </summary>
    /// <param name="route">The page.</param>
    /// <param name="replace">Replace the entry of the history instead of adding one.</param>
    public void Navigate(ViewerRoute route, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(route);
        Location.NavigateTo(route.Location, replace);
    }

    /// <summary>
    /// Whether the sheet open now was opened by <see cref="OpenInSheet"/>, so closing it goes back in the history; a sheet that came with the address
    /// (a link, a reload) does not.
    /// </summary>
    public bool SheetPushed { get; private set; }

    /// <summary>
    /// Shows a record in a sheet over the current page (the patient summary): a new entry of the history, so Back closes it.
    /// </summary>
    /// <param name="record">The page of the record.</param>
    /// <example>
    /// <code>
    /// session.OpenInSheet(new ViewerRoute(ViewerPage.Resource, "Observation", "o1"));
    /// </code>
    /// </example>
    public void OpenInSheet(ViewerRoute record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (ViewerRoutes.Parse(Location.Location) is { } current)
        {
            SheetPushed = true;
            Navigate(current.WithPeek(record));
        }
    }

    /// <summary>
    /// Closes the sheet. A sheet <see cref="OpenInSheet"/> opened is closed by going back in the history, which the host does when this returns
    /// <see langword="true"/>; any other is closed here by replacing the address.
    /// </summary>
    /// <returns><see langword="true"/> when the host should go back in the history.</returns>
    /// <example>
    /// <code>
    /// if (session.CloseSheet()) await js.InvokeVoidAsync("history.back");
    /// </code>
    /// </example>
    public bool CloseSheet()
    {
        if (SheetPushed)
        {
            SheetPushed = false;
            return true;
        }

        if (ViewerRoutes.Parse(Location.Location) is { Peek: not null } route)
        {
            Navigate(route.WithPeek(null), replace: true);
        }

        return false;
    }

    // Forgets that the sheet was opened here once the summary has none (Back closed it)
    internal void OnLocationChanged()
    {
        if (ViewerRoutes.Parse(Location.Location) is { Page: ViewerPage.Summary, Peek: null })
        {
            SheetPushed = false;
        }

        Remember();
    }

    private const int RecentCount = 20;

    private readonly List<string> _recent = [];

    // The records opened in this session, the last first, as references (Patient/123): a reference field offers them. In memory only, of this server
    internal IReadOnlyList<string> RecentRecords => _recent;

    private void Remember()
    {
        var route = ViewerRoutes.Parse(Location.Location);
        foreach (var opened in new[] { route?.Peek, route })
        {
            if (opened is { Page: ViewerPage.Resource or ViewerPage.Summary, ResourceType: { } type, Id: { } id })
            {
                var reference = $"{type}/{id}";
                _recent.Remove(reference);
                _recent.Insert(0, reference);
                if (_recent.Count > RecentCount)
                {
                    _recent.RemoveAt(_recent.Count - 1);
                }
            }
        }
    }
}
