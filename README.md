# FireChart FHIR® Viewer

**Readable FHIR® data in your own application.** FireChart Viewer shows what a FHIR server holds about patients: the resource types of the
server, searchable lists, each resource as a readable card or as its source, and a **clinical summary** of one patient (problems, medications,
allergies, results, encounters and more, on one page). It is read-only, signs in with **SMART on FHIR**, speaks FHIR STU3, R4, R4B, R5 and R6,
and comes in English, German, Spanish, Russian and Chinese.

You can use it in two ways:

| | How | For |
|---|---|---|
| **As a frame** | Serve the Viewer (a Docker image of static files) and put it into any web page with an `<iframe>`, optionally driven by a small JavaScript SDK | Any web application: plain HTML, React, Angular, Vue, an EHR or a portal |
| **As a Blazor component** | Reference the projects and render `<FhirViewer>` | Blazor WebAssembly and .NET MAUI Blazor Hybrid applications |

**Where the data goes.** The Viewer runs entirely in the browser (Blazor WebAssembly). It talks to the FHIR server directly: patient data
and access tokens never pass through a server of the Viewer or of its host. The Viewer itself is static files; it has no backend and no
database.

**Try it:** <https://viewer.firechart.org> is a sandbox with public test servers (synthetic patients) in one click, an editor of the
configuration and the code to copy. Use your own instance for real data.

---

## Contents

- [Quick start](#quick-start)
- [Embed in a web page](#embed-in-a-web-page) — serve it, plain iframe, JavaScript SDK, React
- [Configuration (`viewer.json`)](#configuration-viewerjson)
- [Addresses inside the Viewer](#addresses-inside-the-viewer)
- [Use from Blazor](#use-from-blazor)
- [SMART on FHIR sign-in](#smart-on-fhir-sign-in)
- [Requirements of the FHIR server and the browser](#requirements-of-the-fhir-server-and-the-browser)
- [Build, test, repository layout](#build-test-repository-layout)
- [License, contributions, trademarks](#license-contributions-trademarks)

---

## Quick start

With Docker (Compose v2); the image is built from these sources, the first build takes several minutes:

```bash
git clone https://github.com/firechart/viewer.git
cd viewer
docker compose up --build
```

Open <http://localhost:8080> for the sandbox and <http://localhost:8080/embed> for the frame itself (by default it shows the open SMART
Health IT test server).

Without Docker (the .NET 10 SDK):

```bash
dotnet run --project src/FireChart.Viewer.App
```

> On Windows, if Compose asks for `--allow=fs.read=Dockerfile`, the drive letter of the current folder is lowercase (`c:\`), a bug of
> Docker Compose: change to the folder with an uppercase letter (`cd /d C:\...`) or run `docker build -t firechart-viewer:local .`.

---

## Embed in a web page

The examples use the public instance `https://viewer.firechart.org`. Replace it with the base URL of your own container (or of any other
place where the Viewer is served): every address below (`/embed`, `/sdk/viewer.js`, `/smart/callback`, ...) is relative to that base.

- The public instance is a **demo**: it accepts the commands of the SDK from any page (`"frameAncestors": ["*"]` in its `host.json`) and
  shows synthetic data of public test servers. It is fine to try the examples; it is not for real patient data.
- **For real data, serve your own instance** and list only your origins in its `host.json`: the SDK drives a frame only from the origins
  listed there.

### 1. Serve the Viewer

The image is nginx with the published static files, not root, port 8080. Two files configure it; mount your own versions:

```bash
docker build -t firechart-viewer:local .
docker run -d --name fhir-viewer -p 8080:8080 \
  -v "$PWD/viewer.json:/usr/share/nginx/html/viewer.json:ro" \
  -v "$PWD/host.json:/usr/share/nginx/html/host.json:ro" \
  firechart-viewer:local
```

or, with Compose, add to the service in `docker-compose.yml`:

```yaml
    volumes:
      - ./viewer.json:/usr/share/nginx/html/viewer.json:ro
      - ./host.json:/usr/share/nginx/html/host.json:ro
```

- **`viewer.json`**: the FHIR server, SMART, language, appearance ([reference below](#configuration-viewerjson)). It is read at every load of
  the page, so a change needs no restart.
- **`host.json`**: the origins of the pages that may **drive** the frame with messages, in the syntax of CSP `frame-ancestors`:

  ```json
  { "frameAncestors": ["'self'", "https://emr.example.org", "https://*.portal.example.org"] }
  ```

Put a reverse proxy (nginx, Caddy, Traefik, an ingress) in front of the container. It must provide:

- **HTTPS**: SMART and the browsers require it;
- **`Content-Security-Policy: frame-ancestors ...`** with the same list as `host.json`: without it *any* page may frame the Viewer;
- caching headers as you like (the files under `_framework/` are fingerprinted).

The addresses the image serves:

| Address | What it is |
|---|---|
| `/` | The sandbox (test servers, configuration editor, code to copy) |
| `/embed` | The Viewer, the whole page: the address you put into a frame |
| `/sdk/viewer.js`, `/sdk/viewer.d.ts` | The JavaScript SDK of the same version as the frame, and its TypeScript types |
| `/smart/callback` | The way back of a SMART sign-in by redirect |
| `/_content/FireChart.Viewer/smart-callback.html` | The way back of a SMART sign-in in a popup |

### 2. A plain iframe

The simplest integration: the frame shows the server of `viewer.json`.

```html
<iframe src="https://viewer.firechart.org/embed"
        title="FHIR Viewer"
        style="width: 100%; height: 800px; border: 0"></iframe>
```

An address inside the Viewer can follow `/embed/`, e.g. `https://viewer.firechart.org/embed/summary/123` opens the clinical summary of patient
`123` ([addresses](#addresses-inside-the-viewer)).

If the frame is `sandbox`ed, allow `allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox` (the last two
for a SMART sign-in in a popup).

### 3. The JavaScript SDK

The SDK creates the frame, sends it the configuration (so `viewer.json` is not needed), turns its messages into events and promises, and
sizes the frame. One ES module, no dependencies, no build step. Its source is
[`src/FireChart.Viewer.App/wwwroot/sdk`](src/FireChart.Viewer.App/wwwroot/sdk) (`viewer.js` and its TypeScript types `viewer.d.ts`);
every instance serves the copy of its own version at `<base URL>/sdk/viewer.js` and `<base URL>/sdk/viewer.d.ts`. Import it from your
instance, so the SDK and the frame always speak the same protocol, or copy both files into your sources.

```html
<div id="chart" style="height: 800px"></div>
<script type="module">
  import { Viewer } from "https://viewer.firechart.org/sdk/viewer.js";

  const viewer = Viewer.create(
    {
      server: { url: "https://r4.smarthealthit.org", name: "Test server" },
      appearance: { theme: "inherit" },      // follow the light/dark mode of your page
      locale: "inherit"                      // follow lang of your <html>
    },
    { url: "https://viewer.firechart.org/" });  // where the Viewer is served

  viewer.on("ready", e => console.log("ready", e.server));
  viewer.on("navigate", e => console.log("now at", e.location));
  viewer.on("error", e => console.warn(e.code, e.param, e.message));
  viewer.mount("#chart");

  await viewer.navigate("summary/13a00221-8417-4973-961e-e21b32ece95a");
  await viewer.update({ appearance: { variables: { colorPrimary: "#0a7f5a" } } });
</script>
```

The origin of your page must be in `host.json` of that instance, or the frame ignores the commands.

| Member | What it does |
|---|---|
| `Viewer.create(config, options)` | A Viewer. `config`: the keys of `viewer.json`. `options`: `{ url, height?, title?, urlParam? }` |
| `mount(target)` | Puts the frame into an element, or into the first element of a selector |
| `on(event, handler)`, `off(...)` | Events `ready`, `navigate`, `restarting`, `updated`, `error`; `on` returns the function that unsubscribes |
| `navigate(location, { replace })` | Goes to an [address of the Viewer](#addresses-inside-the-viewer) |
| `update(changes)` | Changes `locale`, `appearance`, `terminology`, `clinicalSummary` without a restart; resolves with `{ applied }` |
| `restart(config)` | Starts anew with another configuration (another server, another SMART client) |
| `configure(config)` | Updates or restarts, whichever the difference needs: for the props of a component |
| `print()` | Opens the print dialog for the page of the Viewer only |
| `location`, `options` | The current address of the Viewer and the configuration in effect |
| `destroy()` | Removes the frame |

- `height: "fill"` (default) fills the element, so give the element a height; `height: "auto"` makes the frame as high as its content.
- `urlParam: "fhir"` keeps the address of the Viewer in the address of your page (`?fhir=summary/123`): links and reloads open the Viewer
  where it was.
- A refused request rejects its promise with a `ViewerError` (`type`, `code`, `param`, `message`): `update({ server })` is
  `restart_required`, `navigate("https://...")` is `invalid_location`, a misspelt key is `unknown_option` with a hint
  (`Did you mean 'appearance'?`).

**React** (Angular and Vue are as short: see [HOWTO.md](src/FireChart.Viewer.App/Sdk/HOWTO.md)):

```tsx
import { useEffect, useRef } from "react";
import { Viewer, type ViewerConfig } from "./viewer";   // sdk/viewer.js + viewer.d.ts copied into your sources

export function FhirViewer({ url, config }: { url: string; config: ViewerConfig }) {
  const host = useRef<HTMLDivElement>(null);
  const viewer = useRef<Viewer | null>(null);

  useEffect(() => {
    const instance = Viewer.create(config, { url }).mount(host.current!);
    viewer.current = instance;
    return () => instance.destroy();                     // StrictMode mounts twice in development
  }, [url]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => { viewer.current?.configure(config).catch(console.error); }, [config]);

  return <div ref={host} style={{ height: "100%" }} />;
}
```

In Next.js mark the component `"use client"`.

---

## Configuration (`viewer.json`)

The same object is `viewer.json` of an instance and the `config` of the SDK. Only `server.url` is required. JSON with comments is accepted.

```json
{
  "server": { "url": "https://fhir.example.org/r4", "name": "Our FHIR server", "fhirRelease": "R4", "pageSize": 50 },
  "smart": {
    "clientId": "viewer",
    "scopes": "openid fhirUser launch/patient patient/*.rs",
    "flow": "popup"
  },
  "locale": "auto",
  "terminology": { "server": "https://tx.fhir.org/r4" },
  "clinicalSummary": { "form": "default" },
  "appearance": {
    "theme": "auto",
    "variables": { "colorPrimary": "#0a7f5a", "borderRadius": "6px" },
    "rules": { ".Banner": { "backgroundColor": "#f1f5f9" } }
  }
}
```

| Key | Meaning |
|---|---|
| `server.url` | The base address of the FHIR API. **Required** |
| `server.name` | The name shown in the title and the breadcrumbs (default: the host name) |
| `server.fhirRelease` | `STU3`, `R4`, `R4B`, `R5` or `R6`, used when the server does not answer `/metadata` (default: read from `/metadata`) |
| `server.pageSize` | Resources per page of a list (default 50) |
| `smart` | Present when the server needs a sign-in. A public client with PKCE: there is no secret |
| `smart.clientId` | The client id registered at the authorization server. Required within `smart` |
| `smart.scopes` | The scopes asked for (e.g. `openid fhirUser launch/patient patient/*.rs`) |
| `smart.flow` | `redirect` (default) or `popup`: see [SMART](#smart-on-fhir-sign-in) |
| `smart.authority` | The OpenID authority, when it is not found by the SMART discovery of the server (`.well-known/smart-configuration`) |
| `smart.extraParameters` | More parameters of the authorization request, as a query (`prompt=login&kc_idp_hint=x`) |
| `locale` | `auto` (the language of the browser), `en`, `de`, `es`, `ru`, `zh-Hans`, with a region if wanted (`de-AT`); in the SDK also `inherit` |
| `terminology.server` | A FHIR terminology server that turns bare codes into words; none: codes are shown as they are. `tx.fhir.org` is a public demo, without licensed code systems |
| `clinicalSummary.form` | The form of the clinical summary (its blocks and their searches); `default` is the built-in one |
| `appearance.theme` | `light`, `dark`, `auto` (the operating system); in the SDK also `inherit` (the mode of your page) |
| `appearance.variables` | `colorPrimary`, `colorBackground`, `colorSurface`, `colorText`, `colorTextSecondary`, `colorBorder`, `colorDanger`, `colorWarning`, `colorSuccess`, `colorInfo`, `fontFamily`, `borderRadius` |
| `appearance.rules` | Properties for the public selectors `.Banner`, `.Tile`, `.Badge`, `.Card`, `.Table` (and their `:hover`): colours, borders, radius, shadow, font, spacing |

The configuration is checked strictly: an unknown key, a missing one or a wrong value is shown in the page with its path and a hint
(`'apperance' is not an option. Did you mean 'appearance'?`), and the Viewer does not start with a wrong configuration. A value of the
appearance it cannot use is left out and reported (`error` event).

---

## Addresses inside the Viewer

The same addresses work after `/embed/`, in `navigate()`, in the `navigate` event and below the `RoutePrefix` of the Blazor component:

| Address | Page |
|---|---|
| (empty), `resources` | The resource types of the server |
| `resources/Patient` | A list; a FHIR search may follow: `resources/Observation?code=8867-4&patient=123` |
| `resources/Patient/123` | One resource (View and Source tabs) |
| `summary` | The choice of a patient for the clinical summary |
| `summary/123` | The clinical summary of patient `123` |

Only relative addresses of the Viewer are accepted (no scheme, host, `..` or encoded separators).

---

## Use from Blazor

`<FhirViewer>` is a Razor component for **Blazor WebAssembly** and **.NET MAUI Blazor Hybrid**. It does not work on Blazor Server, in static
SSR or with prerendering (it shows an error there): the data of a patient and the tokens must stay on the device.

> NuGet packages are planned but not published yet. Until then, reference the projects of this repository (a git submodule, or a copy of
> `src/`). `src/FireChart.Viewer.App` is a complete working host: the code below is the minimum taken from it.

**1. References** (your `.csproj`, Blazor WebAssembly):

```xml
<ItemGroup>
  <PackageReference Include="Radzen.Blazor" Version="12.2.0" />
  <ProjectReference Include="viewer/src/FireChart.Viewer/FireChart.Viewer.csproj" />
  <ProjectReference Include="viewer/src/FireChart.Viewer.Core/FireChart.Viewer.Core.csproj" />
  <ProjectReference Include="viewer/src/FireChart.Viewer.Library/FireChart.Viewer.Library.csproj" />
  <ProjectReference Include="viewer/src/Flox.Core/Flox.Core.csproj" />
  <ProjectReference Include="viewer/src/Flox.Shared/Flox.Shared.csproj" />
</ItemGroup>
<!-- Optional: the FHIR SDKs of releases other than R4 are downloaded only when a server of that release is opened -->
<ItemGroup>
  <BlazorWebAssemblyLazyLoad Include="Hl7.Fhir.STU3.wasm;Hl7.Fhir.R4B.wasm;Hl7.Fhir.R5.wasm;Hl7.Fhir.R6.wasm" />
</ItemGroup>
```

**2. Services.** The Viewer is built on the client side of the Flox platform, set up by a bootstrapper. Copy into your project, from
`src/FireChart.Viewer.App`: `ViewerAppBootstrapper.cs` (the Flox client without an application server: an anonymous user of your app, the
Viewer's services), `VendorTemplateRenderers.cs` (the built-in templates), `wwwroot/appsettings.json` (the bootstrapper reads it at the
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
`Services.AddScoped<ViewerAppConfigLoader>()` (it reads `viewer.json`) and `Services.AddScoped<Sandbox.DemoPresets>()` (the sandbox), and
rename the namespace.

**3. Styles and Radzen.** The Viewer takes the look of the host. In `wwwroot/index.html`, in this order:

```html
<link rel="stylesheet" href="_content/Flox.Shared/app.css" />
<link rel="stylesheet" href="_content/Flox.Shared/css/Flox.Shared.Tailwind.css" />
<link rel="stylesheet" href="_content/FireChart.Viewer/css/FireChart.Viewer.Tailwind.css" />
<link rel="stylesheet" href="YourApp.styles.css" />
<script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
```

and in `App.razor`, before the router: `<RadzenTheme Theme="standard"/>` and `<RadzenComponents/>`.

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
            SmartRedirectUri="@NavigationManager.ToAbsoluteUri("smart/callback").ToString()"
            SmartFlow="SmartFlow.Redirect"
            TerminologyServer="https://tx.fhir.org/r4"
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
| `SmartRedirectUri` | Your callback route for a SMART redirect; without it a server that needs a sign-in cannot be read |
| `SmartFlow` | `Redirect` (default) or `Popup` |
| `SmartPopupRedirectUri` | The return page of the popup; default `_content/FireChart.Viewer/smart-callback.html` of your app |
| `TerminologyServer` | Words for bare codes; none: codes as they are |
| `ClinicalSummaryForm` | The form of the clinical summary (`default`) |
| `Appearance` | `ViewerAppearance`: `Theme` (`Inherit` by default: your app owns the theme; `Light`, `Dark`, `Auto` make the Viewer own it), `Variables`, `Rules`, `Fonts`, `AdvancedStylesheet` |
| `ShowBreadcrumbs` | The chain server > type > id above the page (on by default) |
| `OnConfigError` | Called for an appearance value that was left out |

**5. The SMART callback** (only for servers that need a sign-in). Copy `Pages/SmartCallbackPage.razor` of the app and change its targets to
your prefix: at `smart/callback` it completes the sign-in with `ISmartAuthorizer.CompleteAsync(NavigationManager.Uri)` and navigates to the
clinical summary of the patient of the launch context, or back where the sign-in began.

The routing of the component is experimental: it has a small router of its own below `RoutePrefix`, which may change in a later version.

---

## SMART on FHIR sign-in

A server with `smart` (SDK, `viewer.json`) or `AuthenticationType.OAuth2` (Blazor) asks for a SMART sign-in: authorization code with PKCE
`S256`, a public client, no secret. Register **both** redirect URIs of your instance at the authorization server:

| Flow | Redirect URI (Viewer at `https://viewer.firechart.org`) |
|---|---|
| Redirect | `https://viewer.firechart.org/smart/callback` |
| Popup | `https://viewer.firechart.org/_content/FireChart.Viewer/smart-callback.html` |

- **Redirect** (default): the frame or the page goes to the sign-in page and comes back. Many authorization servers refuse to be shown in a
  frame (`X-Frame-Options`, CSP), so in a frame it often fails.
- **Popup**: the sign-in opens in a window of its own (a new tab on phones), the frame stays and gets the result. **Use it when the Viewer
  is in a frame.** It needs a click (browsers allow windows only at a click), `allow-popups` on a sandboxed frame, and no
  `Cross-Origin-Opener-Policy: same-origin` on your page (`same-origin-allow-popups` is fine). When the window cannot open, the Viewer says
  why and offers to try again or to sign in inside the frame.
- A sign-in **with patient context** (scope `launch/patient`) opens the clinical summary of the patient the user chose or was launched with.
- The token lives in the storage of the frame's tab (partitioned by the browser in a cross-site frame); there is no silent renewal: when it
  ends, the user signs in again.
- Testing: the SMART App Launcher (`https://launch.smarthealthit.org/v/r4/fhir`, any client id, extra parameters `launch=e30` for a
  standalone launch).

Every platform, the risks and a troubleshooting table: [src/FireChart.Viewer/README.md](src/FireChart.Viewer/README.md#smart-sign-in).

---

## Requirements of the FHIR server and the browser

- **CORS.** The browser calls the FHIR server (and the terminology server) from the origin of the Viewer: the server must allow that origin
  (`Access-Control-Allow-Origin`, and `Authorization` among the allowed headers for SMART).
- **HTTPS.** A Viewer served over HTTPS cannot call an `http://` server (mixed content), except `localhost`.
- **Browsers:** current Chrome, Edge, Firefox, Safari (desktop and mobile). The first visit downloads about 16 MB (Brotli), cached afterwards;
  the start takes about a second, the rest is the time of the FHIR server.
- **Read-only:** the Viewer never writes to the FHIR server.
- **Not a medical device.** It displays data from other systems; clinical decisions belong to the source systems and to professional
  judgement. The completeness of what a server returns depends on that server and on the scopes granted.

---

## Build, test, repository layout

```bash
dotnet build FireChart.Viewer.slnx
dotnet test FireChart.Viewer.slnx
docker compose up --build          # the image, as published
```

| Project | What it is |
|---|---|
| `src/FireChart.Viewer` | The Razor component `<FhirViewer>`, its pages, SMART, appearance ([README](src/FireChart.Viewer/README.md)) |
| `src/FireChart.Viewer.Core` | The engine: FHIR client per release, Liquid (Fluid) rendering, clinical summary forms, SMART authorizer ([README](src/FireChart.Viewer.Core/README.md)) |
| `src/FireChart.Viewer.Library` | The built-in templates of the resources and the vendor clinical summary forms ([README](src/FireChart.Viewer.Library/README.md)) |
| `src/FireChart.Viewer.App` | The application: sandbox, `/embed`, the JavaScript SDK in `wwwroot/sdk` ([README](src/FireChart.Viewer.App/README.md), [SDK](src/FireChart.Viewer.App/Sdk/README.md), [HOWTO](src/FireChart.Viewer.App/Sdk/HOWTO.md)) |
| `src/Flox.Core`, `src/Flox.Shared`, `src/Flox.Resources`, `src/FireChart.Resources` | The client side of the Flox platform the Viewer is built on, and the texts |
| `test/` | Unit tests of the Viewer and of the client Flox |
| `samples/` | An HTML page that uses the SDK |

The application is not compiled with AOT on purpose (it doubles the download and wins nothing measurable): see the
[performance notes](src/FireChart.Viewer.App/README.md#performance).

---

## License, contributions, trademarks

- **License:** [Mozilla Public License 2.0](LICENSE) (MPL-2.0). You may use the Viewer in open and closed applications; changes to the files
  of this repository that you distribute must be published under MPL-2.0. See [NOTICE](NOTICE).
- **Contributions** will be accepted under a Contributor License Agreement (CLA), signed once before the first pull request is merged; the
  CLA is being prepared. Until it is published, please open an issue instead of a pull request.
- **Trademarks:** the license grants no rights to the name FireChart or its logos: a modified version must not be called FireChart.
  FHIR® is the registered trademark of Health Level Seven International (HL7); its use does not constitute endorsement by HL7.
- **Third-party components** keep their own licenses; they are listed on the About page of the application.
