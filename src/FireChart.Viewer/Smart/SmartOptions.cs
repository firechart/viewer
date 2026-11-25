namespace FireChart.Viewer.Smart;

/// <summary>
/// How a component of the Viewer signs in to a FHIR server with SMART (OAuth2): the way the sign-in leaves the page and the addresses it comes back
/// to. One value for <see cref="FhirViewer.Smart"/>, <see cref="Bundles.FhirBundleVisualizer.Smart"/> and <see cref="ServerSignInPanel.Smart"/>.
/// </summary>
/// <remarks>
/// Register <see cref="RedirectUri"/> and the page of <see cref="PopupRedirectUri"/> with the authorization servers. See the section "SMART sign-in"
/// of the README of <c>FireChart.Viewer</c> for the flows on each platform, the risks and the troubleshooting.
/// </remarks>
/// <example>
/// <code>
/// &lt;FhirViewer Server=@server Smart=@(new SmartOptions { RedirectUri = Navigation.ToAbsoluteUri("smart/callback").ToString(), Flow = SmartFlow.Popup })/&gt;
/// </code>
/// </example>
public sealed record SmartOptions
{
    /// <summary>No sign-in by redirect (no callback route), the popup with the page of this package: a server that needs a sign-in can be read only
    /// by the popup.</summary>
    /// <example>
    /// <code>
    /// var smart = options ?? SmartOptions.Default;
    /// </code>
    /// </example>
    public static readonly SmartOptions Default = new();

    /// <summary>How the sign-in leaves the page: <see cref="SmartFlow.Redirect"/> (the default) or <see cref="SmartFlow.Popup"/> (a window of its own,
    /// for a frame whose authorization server refuses to be framed; the page stays).</summary>
    /// <example>
    /// <code>
    /// new SmartOptions { Flow = SmartFlow.Popup, RedirectUri = callback }
    /// </code>
    /// </example>
    public SmartFlow Flow { get; init; } = SmartFlow.Redirect;

    /// <summary>The callback route of the host for a sign-in by redirect, absolute, as registered with the authorization servers (FireChart: its route
    /// <c>smart/callback</c>). None: a sign-in by redirect cannot be made, and with <see cref="SmartFlow.Redirect"/> a server that needs one cannot
    /// be read; with the popup it is the way out when the window cannot be used.</summary>
    /// <example>
    /// <code>
    /// new SmartOptions { RedirectUri = Navigation.ToAbsoluteUri("smart/callback").ToString() }
    /// </code>
    /// </example>
    public string? RedirectUri { get; init; }

    /// <summary>The return page of a sign-in in a popup, absolute, on the origin of the application; none:
    /// <c>_content/FireChart.Viewer/smart-callback.html</c> below the base address of the application.</summary>
    /// <example>
    /// <code>
    /// new SmartOptions { Flow = SmartFlow.Popup, PopupRedirectUri = "https://app.example.org/signed-in.html" }
    /// </code>
    /// </example>
    public string? PopupRedirectUri { get; init; }
}
