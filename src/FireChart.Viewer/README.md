# FireChart.Viewer

FireChart Viewer: a Blazor component that browses the data of a FHIR server from the client (resource types, lists, resources, the patient summary of
a patient). It talks to the FHIR server directly, draws resources with the components of the library (`components/{Name}.xml`, `FireChart.Viewer.Library`), and is built on the client
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

## A Blazor WebAssembly host from scratch

The component needs the client side of Flox set up by a bootstrapper. `src/FireChart.Viewer.App` is a complete working host; the
steps below are the minimum taken from it. NuGet packages are planned but not published yet: reference the projects of the repository.

**1. References** (your `.csproj`, Blazor WebAssembly):

```xml
<ItemGroup>
  <ProjectReference Include="src/FireChart.Viewer/FireChart.Viewer.csproj" />
  <ProjectReference Include="src/FireChart.Viewer.Core/FireChart.Viewer.Core.csproj" />
  <ProjectReference Include="src/FireChart.Viewer.Library/FireChart.Viewer.Library.csproj" />
  <ProjectReference Include="src/Flox.Core/Flox.Core.csproj" />
  <ProjectReference Include="src/Flox.Shared/Flox.Shared.csproj" />
</ItemGroup>
<!-- Optional: the FHIR SDKs of releases other than R4 are downloaded only when a server of that release is opened -->
<ItemGroup>
  <BlazorWebAssemblyLazyLoad Include="Hl7.Fhir.STU3.wasm;Hl7.Fhir.R4B.wasm;Hl7.Fhir.R5.wasm;Hl7.Fhir.R6.wasm" />
</ItemGroup>
```

**2. Services.** The Viewer is built on the client side of the Flox platform, set up by a bootstrapper. Copy into your project, from
`src/FireChart.Viewer.App`: `ViewerAppBootstrapper.cs` (the Flox client without an application server: an anonymous user of your app, the
Viewer's services), `wwwroot/appsettings.json` (the bootstrapper reads it at the
start and stops without it: the levels of the log) and, if you lazy-load, `WasmReleaseAssemblyLoader.cs`. Then in `Program.cs`:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

new ViewerAppBootstrapper(builder.Services, builder.Configuration, builder.Logging,
                          builder.HostEnvironment.BaseAddress, builder.HostEnvironment.Environment).Configure();
builder.Services.AddSingleton<IReleaseAssemblyLoader, WasmReleaseAssemblyLoader>();   // after the bootstrapper
// The background-task pages of Flox.Shared need an application server; without these lines Development fails its DI validation
builder.Services.RemoveAll<Flox.Shared.Pages.BackgroundTask.BackgroundTasksState>();
builder.Services.RemoveAll<Flox.Shared.Pages.BackgroundTask.BackgroundTaskViewState>();

await builder.Build().RunAsync();
```

In the copied bootstrapper, remove the two registrations that belong to the application of this repository, not to yours:
`Services.AddScoped<ViewerAppConfigLoader>()` (it reads `viewer.json`) and `Services.AddScoped<Sandbox.DemoPresets>()` (the playground), and
rename the namespace.

**3. Styles.** The Viewer takes the look of the host. In `wwwroot/index.html`, in this order:

```html
<link rel="stylesheet" href="_content/Flox.Shared/app.css" />
<link rel="stylesheet" href="_content/Flox.Shared/theme.css" />
<link rel="stylesheet" href="_content/BlazorBlueprint.Components/blazorblueprint.css" />
<link rel="stylesheet" href="_content/Flox.Shared/css/Flox.Shared.Tailwind.css" />
<link rel="stylesheet" href="_content/FireChart.Viewer/css/FireChart.Viewer.Tailwind.css" />
<link rel="stylesheet" href="YourApp.styles.css" />
```

and in `App.razor`, before the router, the hosts of Blazor Blueprint: `<BbPortalHost/>`, `<BbDialogProvider/>` and `<BbToastProvider/>`.

**4. A page.** The host owns one catch-all route; the Viewer navigates below `RoutePrefix`, so links and the Back button work:

```razor
@page "/viewer/{*Path}"
@using FireChart.Viewer
@using FireChart.Viewer.Appearance
@using FireChart.Viewer.Smart
@using FireChart.Viewer.Core.Fhir
@inject NavigationManager NavigationManager

<FhirViewer Server="_server"
            RoutePrefix="viewer/"
            Smart="@(new SmartOptions { RedirectUri = NavigationManager.ToAbsoluteUri("smart/callback").ToString(), Flow = SmartFlow.Redirect })"
            Fhir="@(new FhirOptions { TerminologyServerUrl = "https://tx.fhir.org/r4" })"
            Appearance="_appearance" />

@code {
    [Parameter] public string? Path { get; set; }

    private readonly IViewerServer _server = new ViewerServer("https://r4.smarthealthit.org", "Test server");
    private readonly ViewerAppearance _appearance = new() { Theme = ViewerTheme.Auto, Variables = { ["colorPrimary"] = "#0a7f5a" } };

    private sealed record ViewerServer(string BaseUrl, string Name) : IViewerServer
    {
        public string Key => BaseUrl;                                   // the key of caches and sessions
        public AuthenticationType AuthenticationType => AuthenticationType.None;   // OAuth2 for SMART
        public Hl7.Fhir.Specification.FhirRelease? FhirRelease => null;   // read from /metadata
        public int PageSize => 50;
        // With SMART: AuthenticationType.OAuth2 and OAuth2ClientId, OAuth2Scope (optional: Authority, OAuth2ExtraParameters)
    }
}
```

| Parameter | Meaning |
|---|---|
| `Server` | The FHIR server (`IViewerServer`). **Required**; another server starts a new session |
| `RoutePrefix` | The path of your catch-all route (`viewer/`) |
| `Smart` | `SmartOptions` of a SMART sign-in: `RedirectUri` (your callback route for a redirect; without it a server that needs a sign-in can be read only by the popup), `Flow` (`Redirect`, the default, or `Popup`), `PopupRedirectUri` (the return page of the popup; default `_content/FireChart.Viewer/smart-callback.html` of your app) |
| `Fhir` | `FhirOptions`, the FHIR settings: `TerminologyServerUrl` (words for bare codes; none: codes as they are), `PageSize` and `LoadAll` of a patient summary, `Decimals` (the most decimal places of a value, 2) |
| `Appearance` | `ViewerAppearance`: `Theme` (`Inherit` by default: your app owns the theme; `Light`, `Dark`, `Auto` make the Viewer own it), `Variables`, `Rules`, `Fonts`, `AdvancedStylesheet` |
| `ShowBreadcrumbs` | The chain server > type > id above the page (on by default) |
| `OnConfigError` | Called for an appearance value that was left out |

**5. The SMART callback** (only for servers that need a sign-in). Copy `Pages/SmartCallbackPage.razor` of the app and change its targets to
your prefix: at `smart/callback` it completes the sign-in with `ISmartAuthorizer.CompleteAsync(NavigationManager.Uri)` and navigates to the
patient summary of the patient of the launch context, or back where the sign-in began.

## Bundle Visualizer

`FhirBundleVisualizer` is the second component of the package: a FHIR Bundle drawn as the graph of the references between its resources. It needs no
server and no route.

```razor
<FhirBundleVisualizer Bundle="@_pastedJsonOrXml" />
<FhirBundleVisualizer Server="_server" Location="Patient/123/$everything" />
```

- `Bundle` is the text of a Bundle, JSON or XML (pasted, dropped, read by the host). A pasted Bundle stays in the browser: the resources it refers to
  and does not hold are drawn dashed and are not read from anywhere.
- `Location` is the address of a Bundle (a search, `$everything`, a document), absolute or relative to `Server`. It is read once as JSON; the credentials
  of `Server` go only to its own origin. A `Server` with SMART (OAuth2) is signed in to first, with the panel of the Viewer: `Smart` as for
  `FhirViewer` (the session of the tab is reused; a token the server refuses with 401 is forgotten and the sign-in asked again). A redirect leaves
  the page: the host gives `Location` again when it comes back (the SDK does), or uses `SmartFlow.Popup`, which keeps the page.
- The edges are the references of type `Reference` (also the one in a `CodeableReference`), found by the member `reference` of the JSON and
  resolved as the specification says (*Resolving references in Bundles*): an absolute one or a `urn:uuid:` by the `fullUrl` of an entry, a relative
  one against the base of the `fullUrl` of the entry that holds it, else by type and id. A reference to a contained resource is not an edge.
  References of type `canonical` (`QuestionnaireResponse.questionnaire`, `instantiatesCanonical`, much used in R5) are not edges yet.
- What refers to nothing (a Patient) is on top, what refers to it below; a long layer wraps into rows. A drag pans, the wheel and a pinch zoom, the
  types of the toolbar hide and show their resources.
- A click or a tap on a resource opens its sheet: **Card** (the record as the sheet of the Viewer shows it: `{Type}.page` of the library, else its banner and `{Type}.view`; its references read from the Bundle; and its references either way) and
  **Source** (its JSON). The sheet can show the neighbourhood of the resource (two references either way); `OnSelect` tells the host what was chosen.
- While it reads and resolves, the dialog of a long loading (`LoadingPanel` of `Flox.Shared`, the one of a record loaded at once) says what it does and has Cancel.
  `OnLoad` reports every Bundle given once it is drawn or could not be (`BundleLoad`: the counts, or `invalid_bundle` / `unavailable` /
  `cancelled` with the text shown); `ReloadAsync()` reads it anew (the same parameters given again do not); `Appearance` and `OnConfigError` are
  those of the Viewer.

The host needs the same services as for the Viewer; the Bundle Visualizer has its height from its parent (give the parent one). Another application
embeds it as it embeds the Viewer: the frame `/embed-bundle` of `FireChart.Viewer.App` and `BundleVisualizer` of its JavaScript SDK.

## Styles

The Viewer has no styles of its own beyond its utilities: it takes the look of the host. In a host that is not FireChart, link, in this order:

```html
<link rel="stylesheet" href="_content/Flox.Shared/app.css" />
<link rel="stylesheet" href="_content/Flox.Shared/theme.css" />
<link rel="stylesheet" href="_content/BlazorBlueprint.Components/blazorblueprint.css" />
<link rel="stylesheet" href="_content/Flox.Shared/css/Flox.Shared.Tailwind.css" />
<link rel="stylesheet" href="_content/FireChart.Viewer/css/FireChart.Viewer.Tailwind.css" />
<link rel="stylesheet" href="{YourApp}.styles.css" />
```

register the components with `services.AddFloxComponents()` and put `<BbPortalHost/>`, `<BbDialogProvider/>` and `<BbToastProvider/>` of Blazor
Blueprint into the root component. `FireChart.Viewer.App` (in the repository) is a complete example of such a host.

`Appearance` with a theme (`Light`, `Dark`, `Auto`) makes the Viewer the owner of the theme of the document: it sets `data-theme` and the class `dark` on `<html>`
(the dark tokens of `theme.css`). Leave the theme
`Inherit` (the default) when the host switches the theme itself, as FireChart does.

## SMART sign-in

A server with `AuthenticationType.OAuth2` asks for a SMART sign-in (authorization code with PKCE `S256`, a public client). The Viewer offers two ways
for the browser to leave the page, chosen by `Flow` of the parameter `Smart` (`SmartOptions`: `Flow`, `RedirectUri`, `PopupRedirectUri`; the same
value for `FhirViewer` and `FhirBundleVisualizer`):

| `Flow` | What happens | Use it when |
|---|---|---|
| `Redirect` (default) | The page (or the frame) goes to the sign-in page of the authorization server and comes back to `RedirectUri`, a route of the host | The Viewer owns its page: a Blazor WebAssembly host, MAUI, a frame whose authorization server allows to be framed |
| `Popup` | The sign-in page opens in a window of its own; the page stays where it is and gets the result when the window closes | The Viewer is in a frame of another application (an EHR, a portal) and the authorization server refuses to be shown in a frame |

### Examples

A Blazor host with the redirect (the host owns the callback route and completes the sign-in there with `ISmartAuthorizer.CompleteAsync`):

```razor
<FhirViewer Server="_server"
            RoutePrefix="@($"server/{Id}/")"
            Smart="@(new SmartOptions { RedirectUri = NavigationManager.ToAbsoluteUri("smart/callback").ToString() })" />
```

The same with the popup. `RedirectUri` stays: it is the way out when the window cannot be used (see *Fallbacks*).

```razor
<FhirViewer Server="_server"
            RoutePrefix="@($"server/{Id}/")"
            Smart="@(new SmartOptions { RedirectUri = NavigationManager.ToAbsoluteUri("smart/callback").ToString(), Flow = SmartFlow.Popup })" />
```

`PopupRedirectUri` sets the return page of the window; by default it is `_content/FireChart.Viewer/smart-callback.html` below the base address of
the application (`<base href>`), a static page of this package. It must be on the origin of the Viewer.

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
   secret is), checks `state`, keeps the session of the tab and reads the page again, or opens the patient summary of the patient of the launch context.

While it waits, the Viewer looks at the window every 0.5 s. It gives up when the window is closed, when it loses the window, or after 10 minutes.

### Fallbacks

The popup never leaves the user stuck: when it cannot work the panel says why and offers two buttons.

| What happened | Message | Buttons |
|---|---|---|
| The browser did not open the window (a blocker, a sandboxed frame, an in-app browser) | *The browser did not open the sign-in window ...* | **Open the sign-in window** (a new click, a new chance), **Sign in here** (the redirect) |
| The window was closed before the end, the Viewer lost it (see COOP below), or 10 minutes passed | *The sign-in window was closed before the sign-in was complete ...* | the same |
| The authorization server refused, or the token request failed | the text of the failure | the same |

**Sign in here** is the redirect of the frame: it needs `RedirectUri` and an authorization server that allows framing. Without them the user can
only try the window again.

### Platforms

What to expect. Only the rows marked *verified* were tried; the rest follows from how the platform is documented to behave and still has to be checked
(see *Test matrix*).

| Platform | Behaviour |
|---|---|
| **Chrome, Edge** (Windows, macOS, Linux) | A separate window. Works from a cross-origin frame as long as the frame is not sandboxed without `allow-popups`. *Verified 2026-11-07* (Chrome, Windows, the test page with the SMART App Launcher): the window opens, closes after the choice of a patient, and the frame shows the patient summary of that patient |
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
| The window closed, the panel did not move on | The message was dropped: the return page is on another origin than the Viewer (a `PopupRedirectUri` on another host, `localhost` against `127.0.0.1`) | Put `PopupRedirectUri` on the origin of the Viewer, not of the host page |
| *The answer of the sign-in server does not match the request* | The same sign-in was completed twice, or the frame reloaded while the window was open | Start again with one click |
| *The server did not give an access token* | The token request from the frame failed: CORS of the token endpoint, or `invalid_grant` (the token request carries the popup redirect URI, which must be registered too) | The network tab of the frame, the request to the token endpoint |
| The window shows HTTP 500 on `smart-callback.html`, only under Visual Studio | The browser refresh of Hot Reload (`Microsoft.AspNetCore.Watch.BrowserRefresh`) fails to unpack the precompressed static page to inject its script (`InvalidDataException: ... unsupported compression method` in the log); a tool of development, not in `dotnet run` or a published app | Turn off *Auto refresh browser* in Visual Studio (Tools → Options → Projects and Solutions → ASP.NET Core), or run with `dotnet run` |
| iOS: after the sign-in the user stays on an empty tab | The browser did not let the page close the tab | Close the tab; the frame is signed in already |

Where to look: the console and the network tab **of the frame** (in Chrome DevTools pick the frame in the context selector of the console), not those of
the host page; and the console of the window before it closes (Chrome DevTools: *Auto-open DevTools for popups*).

### Test matrix

Run it on each platform you support, with the SMART App Launcher (`https://launch.smarthealthit.org/v/r4/fhir`, any client id, extra parameters
`launch=e30`) and a host page on another origin that frames the Viewer app (`samples`), with `Flow = SmartFlow.Popup`:

1. **Sign in** in the frame: a window (a tab on phones) opens with the sign-in of the launcher.
2. Pick a provider and a patient: the window closes and the frame shows the patient summary of that patient.
3. Close the window before the end: the frame shows the *closed* message and the two buttons; **Open the sign-in window** tries again.
4. Block popups for `localhost` in the browser settings: **Sign in** gives the *blocked* message; **Sign in here** completes the redirect inside the frame
   (the launcher allows framing).
5. Reload the frame after the sign-in: no new sign-in (the session of the tab).

## Texts

The texts of the Viewer are those of FireChart (`FireChart.Resources`: English, German, Spanish, Russian, Chinese) and the common ones of Flox
(`Flox.Resources`).

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
