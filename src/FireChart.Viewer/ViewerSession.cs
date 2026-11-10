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
    /// <param name="smartRedirectUri">The callback address of the host for a SMART sign-in, as registered with the authorization servers; none: no sign-in.</param>
    /// <param name="smartFlow">How a SMART sign-in leaves the page.</param>
    /// <param name="smartPopupRedirectUri">The return page of a sign-in in a popup (<c>_content/FireChart.Viewer/smart-callback.html</c>, absolute).</param>
    /// <param name="terminologyServer">The terminology server for the displays of bare codes (<see cref="TerminologyResolver.AddressOf"/>); none: the
    /// codes are shown as they are, nothing of a patient leaves for another server.</param>
    /// <param name="clinicalSummaryForm">The code of the clinical summary form of the server; none is <c>default</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="server"/> or <paramref name="location"/> is <see langword="null"/>.</exception>
    public ViewerSession(IViewerServer server, IViewerLocationStore location, string? smartRedirectUri = null, string? terminologyServer = null, string? clinicalSummaryForm = null,
                         FireChart.Viewer.Smart.SmartFlow smartFlow = FireChart.Viewer.Smart.SmartFlow.Redirect, string smartPopupRedirectUri = "")
    {
        SmartFlow = smartFlow;
        SmartPopupRedirectUri = smartPopupRedirectUri;
        ClinicalSummaryForm = string.IsNullOrWhiteSpace(clinicalSummaryForm) ? null : clinicalSummaryForm.Trim();
        TerminologyServer = TerminologyResolver.AddressOf(terminologyServer);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(location);
        Server = server;
        Location = location;
        SmartRedirectUri = smartRedirectUri;
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
    /// The callback address of the host for a SMART sign-in, or <see langword="null"/>.
    /// </summary>
    public string? SmartRedirectUri { get; }

    /// <summary>
    /// How a SMART sign-in leaves the page.
    /// </summary>
    public FireChart.Viewer.Smart.SmartFlow SmartFlow { get; }

    /// <summary>
    /// The return page of a sign-in in a popup, absolute; register it with the authorization servers next to <see cref="SmartRedirectUri"/>.
    /// </summary>
    public string SmartPopupRedirectUri { get; }

    /// <summary>
    /// The base address of the terminology server, or <see langword="null"/>.
    /// </summary>
    public string? TerminologyServer { get; }

    /// <summary>
    /// The code of the clinical summary form of the server, or <see langword="null"/> for <c>default</c>.
    /// </summary>
    public string? ClinicalSummaryForm { get; }

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
}
