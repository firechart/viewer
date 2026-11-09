// The return page of a SMART sign-in in a popup (SmartFlow.Popup of FireChart Viewer): it passes the address of the way back (code and state) to the
// Viewer that opened the window, on this origin only, and closes. It exchanges nothing: the PKCE secret is in the Viewer, so the code alone is useless here.
(function () {
    var status = document.getElementById("status");
    var message = { type: "firechart-smart-callback", url: window.location.href };
    if (window.opener && !window.opener.closed) {
        try {
            window.opener.postMessage(message, window.location.origin);
            status.textContent = "Signed in. This window closes now.";
            setTimeout(function () { window.close(); }, 300);
            return;
        } catch (e) {
            // the opener is gone or of another origin
        }
    }

    // No opener: the window was opened as a tab without it, or a page of the authorization server cut it (Cross-Origin-Opener-Policy)
    status.textContent = "The sign-in could not be passed back to the application. Close this window, return to the application and choose \"Sign in here\".";
})();
