# FireChart.Viewer

FireChart Viewer: a Blazor component that browses the data of a FHIR server from the client (resource types, lists, resources, the clinical summary of
a patient). It talks to the FHIR server directly, renders resources with the Liquid templates of `FireChart.Viewer.Library`, and is built on the client
side of Flox (`Flox.Core`, `Flox.Shared`).

**Client side only.** It runs on Blazor WebAssembly (`blazor.webassembly.js`) and on Blazor Hybrid (MAUI, `blazor.webview.js`); the data of a patient and the
tokens never pass through a server of the host. On Blazor Server, static SSR or with prerendering it shows an error instead of working.

## Use

```razor
@page "/server/{Id:guid}/{*Path}"

<FhirViewer Server="_server" RoutePrefix="@($"server/{Id}/")" />
```

Above the page the Viewer shows its chain of crumbs: server > type > id (`ShowBreadcrumbs`, on by default; a host with crumbs of its own turns it
off, as FireChart does).

The host owns one catch-all route; the Viewer reads the rest of the address (`resources`, `resources/Patient`, ...) and navigates below `RoutePrefix` with the
`NavigationManager`, so links and the Back button work as usual.

**The routing is experimental.** The Viewer has its own small router instead of `@page` (a route is a compile-time constant: it cannot take a prefix or live
in the address space of another application). It may change, or be replaced by `@page`, in a later version.

## Styles

The Viewer has no styles of its own beyond its utilities: it takes the look of the host. In a host that is not FireChart, link, in this order:

```html
<link rel="stylesheet" href="_content/Flox.Shared/app.css" />
<link rel="stylesheet" href="_content/Flox.Shared/css/Flox.Shared.Tailwind.css" />
<link rel="stylesheet" href="_content/FireChart.Viewer/css/FireChart.Viewer.Tailwind.css" />
<link rel="stylesheet" href="{YourApp}.styles.css" />
<script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
```

and put `<RadzenTheme Theme="standard"/>` and `<RadzenComponents/>` into the root component. `FireChart.Viewer.App` (in the repository) is a complete example of such a host.

`Appearance` with a theme (`Light`, `Dark`, `Auto`) makes the Viewer the owner of the theme of the document: it sets `data-theme` on `<html>` and switches the
base theme of Radzen (`standard` or `standard-dark`) through Radzen's `ThemeService`, so the `RadzenTheme` of the host must be there. Leave the theme
`Inherit` (the default) when the host switches the theme itself, as FireChart does.

## SMART sign-in

A server with `AuthenticationType.OAuth2` asks for a SMART sign-in (authorization code with PKCE `S256`, a public client). The Viewer offers two ways
for the browser to leave the page, chosen by the `SmartFlow` parameter:

| `SmartFlow` | What happens | Use it when |
|---|---|---|
| `Redirect` (default) | The page (or the frame) goes to the sign-in page of the authorization server and comes back to `SmartRedirectUri`, a route of the host | The Viewer owns its page: a Blazor WebAssembly host, MAUI, a frame whose authorization server allows to be framed |
| `Popup` | The sign-in page opens in a window of its own; the page stays where it is and gets the result when the window closes | The Viewer is in a frame of another application (an EHR, a portal) and the authorization server refuses to be shown in a frame |

### Examples

A Blazor host with the redirect (the host owns the callback route and completes the sign-in there with `ISmartAuthorizer.CompleteAsync`):

```razor
<FhirViewer Server="_server"
            RoutePrefix="@($"server/{Id}/")"
            SmartRedirectUri="@NavigationManager.ToAbsoluteUri("smart/callback").ToString()" />
```

The same with the popup. `SmartRedirectUri` stays: it is the way out when the window cannot be used (see *Fallbacks*).

```razor
<FhirViewer Server="_server"
            RoutePrefix="@($"server/{Id}/")"
            SmartRedirectUri="@NavigationManager.ToAbsoluteUri("smart/callback").ToString()"
            SmartFlow="SmartFlow.Popup" />
```

`SmartPopupRedirectUri` sets the return page of the window; by default it is `_content/FireChart.Viewer/smart-callback.html` below the base address of
the application (`<base href>`), a static page of this package. It must be on the origin of the Viewer.

FireChart turns the popup on for its embedded pages (`/app/embed/...`) with a setting of the server; its own pages always use the redirect:

```json
{
  "FireChart": {
    "Embedding": {
      "SmartFlow": "Popup"
    }
  }
}
```

or the environment variable `FireChart__Embedding__SmartFlow=Popup`. Any other value, or none, is `Redirect`.

### Registration at the authorization server

The popup comes back to a **second redirect URI**. Register both:

| Flow | Redirect URI (FireChart at `https://viewer.example.org`) |
|---|---|
| Redirect | `https://viewer.example.org/app/smart/callback` |
| Popup | `https://viewer.example.org/app/_content/FireChart.Viewer/smart-callback.html` |

An authorization server that does not know the second one shows an error such as `invalid_redirect_uri` **inside the window**. The SMART App Launcher
(`launch.smarthealthit.org`) accepts any.

### How the popup works

1. **At the click** the Viewer opens an empty window (`about:blank`, named `firechart-smart`, 520×720). This comes first, before any network call:
   a browser lets a window through only while the click is fresh (*transient user activation*, a few seconds at most).
2. The Viewer reads the discovery document, makes `state` and the PKCE secret, keeps them in the storage of its own tab (the frame), and sets the address
   of the window to the authorization endpoint.
3. The user signs in and consents **in the window**. The window is a top-level page, so the authorization server sees its own cookies (a "remember me"
   works, unlike in a frame of another site) and its `frame-ancestors` / `X-Frame-Options` do not matter.
4. The authorization server sends the window to `smart-callback.html`. That page does nothing but post its own address to `window.opener` with
   `postMessage`, **to its own origin only**, and closes itself. It has no inline script (it works under a strict CSP) and touches no storage.
5. The Viewer accepts the message only from its own origin and with its own type, exchanges the code for the token **in the frame** (where the PKCE
   secret is), checks `state`, keeps the session of the tab and reads the page again, or opens the clinical summary of the patient of the launch context.

While it waits, the Viewer looks at the window every 0.5 s. It gives up when the window is closed, when it loses the window, or after 10 minutes.

### Fallbacks

The popup never leaves the user stuck: when it cannot work the panel says why and offers two buttons.

| What happened | Message | Buttons |
|---|---|---|
| The browser did not open the window (a blocker, a sandboxed frame, an in-app browser) | *The browser did not open the sign-in window ...* | **Open the sign-in window** (a new click, a new chance), **Sign in here** (the redirect) |
| The window was closed before the end, the Viewer lost it (see COOP below), or 10 minutes passed | *The sign-in window was closed before the sign-in was complete ...* | the same |
| The authorization server refused, or the token request failed | the text of the failure | the same |

**Sign in here** is the redirect of the frame: it needs `SmartRedirectUri` and an authorization server that allows framing. Without them the user can
only try the window again.

### Platforms

What to expect. Only the rows marked *verified* were tried; the rest follows from how the platform is documented to behave and still has to be checked
(see *Test matrix*).

| Platform | Behaviour |
|---|---|
| **Chrome, Edge** (Windows, macOS, Linux) | A separate window. Works from a cross-origin frame as long as the frame is not sandboxed without `allow-popups`. *Verified 2026-11-07* (Chrome, Windows, the test page with the SMART App Launcher): the window opens, closes after the choice of a patient, and the frame shows the clinical summary of that patient |
| **Firefox** (desktop) | A separate window (or a tab, by the user's settings). A host that delays the click (an overlay, a confirmation) can get the window blocked |
| **Safari** (macOS) | A separate window. "Block pop-up windows" is on by default but lets a window opened at a click through |
| **Safari, Chrome on iOS / iPadOS** | A **new tab**, not a window. After the sign-in the return page closes the tab and the user is back on the tab of the host. If the tab stays, the user closes it: the sign-in is complete already |
| **Chrome on Android** | A new tab, as on iOS |
| **Android WebView, in-app browsers** (a link opened inside a messenger or a social app) | Usually **no window at all**: the app must handle new windows (`onCreateWindow`), most do not. The panel shows the fallback. Some give a window that never loads; then the user waits until they close it, or for 10 minutes |
| **.NET MAUI (Blazor Hybrid)** | Not applicable: use `Redirect`. The `BlazorWebView` has no windows of its own |
| **An EHR with an embedded browser** (WebView2, CefSharp) | Depends on the EHR: WebView2 and CefSharp open a window only when the host handles `NewWindowRequested` / its life span handler. Ask the EHR vendor, or use `Redirect` with an authorization server that allows framing |
| An embedded Chromium that blocks every popup | *Verified 2026-11-07:* the *blocked* message and the two buttons; **Sign in here** reaches the sign-in page of the SMART App Launcher inside the frame |

### Risks and limits

- **Cross-Origin-Opener-Policy (COOP).** A page with `Cross-Origin-Opener-Policy: same-origin` cuts the link between a window and its opener. If a page
  of the authorization server sends it, or the **top page of the host** does (the COOP of the top page also covers the popups of its frames), the window
  can no longer post back, and to the Viewer it looks closed: the user gets the *closed* message right after the window opens. Fix: the host uses
  `same-origin-allow-popups` or no COOP; for an authorization server that sends it, use the redirect. The first sign-in pages of Epic, Microsoft Entra and
  Oracle Health (Cerner) sent no COOP when checked on 2026-11-07; a later page of their flows may.
- **Sandboxed frames.** A frame with `sandbox` needs `allow-popups` (or no window opens) **and** `allow-popups-to-escape-sandbox` (or the window inherits the
  sandbox and the sign-in page breaks), besides `allow-scripts allow-same-origin allow-forms`.
- **Popup blockers and policies.** A window opened at a click is allowed by default everywhere; strict blockers, enterprise policies
  (`PopupsBlockedForUrls`) and some extensions block it anyway. The fallback covers this.
- **Partitioned storage.** A frame on another site has storage of its own, keyed by the site of the host (Chrome, Safari, Firefox). The session of the
  frame is not seen in another tab of the same application, and the other way round. The popup does not change that: the token lives in the frame.
- **One sign-in at a time.** All Viewers of a browser use the same window name: two frames that sign in at once share the window and the second one wins.
- **No silent renewal.** When the token ends, the user signs in again (a new click, a new window).
- **The return page is public.** `smart-callback.html` is a static file; it only forwards its own address to a window of the same origin. The code in that
  address is useless without the PKCE secret, which never leaves the frame, and `state` is single-use.

### Troubleshooting

| Symptom | Likely cause | What to do |
|---|---|---|
| *The browser did not open the sign-in window* every time | A popup blocker, a policy, a sandboxed frame without `allow-popups`, an in-app browser | Allow popups for the site of the Viewer; check the `sandbox` of the `<iframe>`; open the page in a real browser; or **Sign in here** |
| *The sign-in window was closed ...* right after it opened | COOP: the host page or a page of the authorization server sends `Cross-Origin-Opener-Policy: same-origin` | DevTools → Network → the response headers of the top page and of each page in the window; set `same-origin-allow-popups` on the host |
| The window shows an error of the authorization server: `invalid_redirect_uri`, *redirect URI mismatch* | The return page of the popup is not registered | Register `.../_content/FireChart.Viewer/smart-callback.html` (see above); scheme, host, port and path must match exactly |
| The SMART App Launcher answers `Invalid launch options: SyntaxError: Unexpected end of JSON input` | A standalone sign-in to the launcher without its launch settings | Add `launch=e30` to the OAuth2 extra parameters of the server (`e30` is `{}` in Base64) |
| The window stays on *... choose "Sign in here"* | The return page has no opener: COOP somewhere in the chain, or the page was opened by hand | Close it and use **Sign in here**; look for COOP as above |
| The window stays blank | The discovery or the authorization endpoint is slow or failing, or an in-app browser that never loads windows | The network tab of the frame (`.well-known/smart-configuration`); close the window, the panel offers the fallback |
| The window closed, the panel did not move on | The message was dropped: the return page is on another origin than the Viewer (a `SmartPopupRedirectUri` on another host, `localhost` against `127.0.0.1`) | Put `SmartPopupRedirectUri` on the origin of the Viewer, not of the host page |
| *The answer of the sign-in server does not match the request* | The same sign-in was completed twice, or the frame reloaded while the window was open | Start again with one click |
| *The server did not give an access token* | The token request from the frame failed: CORS of the token endpoint, or `invalid_grant` (the token request carries the popup redirect URI, which must be registered too) | The network tab of the frame, the request to the token endpoint |
| The window shows HTTP 500 on `smart-callback.html`, only under Visual Studio | The browser refresh of Hot Reload (`Microsoft.AspNetCore.Watch.BrowserRefresh`) fails to unpack the precompressed static page to inject its script (`InvalidDataException: ... unsupported compression method` in the log); a tool of development, not in `dotnet run` or a published app | Turn off *Auto refresh browser* in Visual Studio (Tools → Options → Projects and Solutions → ASP.NET Core), or run with `dotnet run` |
| iOS: after the sign-in the user stays on an empty tab | The browser did not let the page close the tab | Close the tab; the frame is signed in already |

Where to look: the console and the network tab **of the frame** (in Chrome DevTools pick the frame in the context selector of the console), not those of
the host page; and the console of the window before it closes (Chrome DevTools: *Auto-open DevTools for popups*).

### Test matrix

Run it on each platform you support, with the SMART App Launcher (`https://launch.smarthealthit.org/v/r4/fhir`, any client id, extra parameters
`launch=e30`) and the test page of FireChart (`https://127.0.0.1:7270/embed-test` with the Viewer from `https://localhost:7270`,
`FireChart:Embedding:SmartFlow` = `Popup`):

1. **Sign in** in the frame: a window (a tab on phones) opens with the sign-in of the launcher.
2. Pick a provider and a patient: the window closes and the frame shows the clinical summary of that patient.
3. Close the window before the end: the frame shows the *closed* message and the two buttons; **Open the sign-in window** tries again.
4. Block popups for `localhost` in the browser settings: **Sign in** gives the *blocked* message; **Sign in here** completes the redirect inside the frame
   (the launcher allows framing).
5. Reload the frame after the sign-in: no new sign-in (the session of the tab).

## Texts

The texts of the Viewer are those of FireChart (`FireChart.Resources`: English, German, Spanish, Russian, Chinese) and the common ones of Flox
(`Flox.Resources`).

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
