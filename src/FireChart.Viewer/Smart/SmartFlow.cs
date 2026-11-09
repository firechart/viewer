namespace FireChart.Viewer.Smart;

/// <summary>
/// How a SMART sign-in leaves the page.
/// </summary>
/// <remarks>
/// <see cref="Redirect"/> suits a Viewer that owns its page (FireChart, a Blazor host, MAUI). <see cref="Popup"/> is for a Viewer in a frame of another
/// application whose authorization server refuses to be shown in a frame: see the section "SMART sign-in" of the README of <c>FireChart.Viewer</c> for the
/// behaviour on each platform, the risks and the troubleshooting.
/// </remarks>
/// <example>
/// <code>
/// &lt;FhirViewer Server="server" SmartRedirectUri="@callback" SmartFlow="SmartFlow.Popup"/&gt;
/// </code>
/// </example>
public enum SmartFlow
{
    /// <summary>The page (or the frame) leaves for the sign-in page and comes back to the callback route of the host; the default.</summary>
    Redirect,

    /// <summary>The sign-in page opens in a window of its own at the click, the page stays; a blocked or lost window falls back to <see cref="Redirect"/>.</summary>
    Popup
}
