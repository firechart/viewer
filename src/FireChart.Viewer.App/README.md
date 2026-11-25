# FireChart.Viewer.App

The application of FireChart Viewer: a Blazor WebAssembly application whose published files any web server can serve (the Docker image below). It shows one FHIR server, set in `viewer.json`.

| Address | What it is |
|---|---|
| `/embed` | The Viewer, the whole page: the frame another application (an EHR, a portal) embeds |
| `/embed-bundle` | The Bundle Visualizer, the whole page: the frame of `BundleVisualizer` of the SDK (the messages below, `show` instead of `navigate`) |
| `/` | The home page: plain HTML (`index.html` starts the runtime only on a route of the application), with the demos `viewer.html` and `bundle.html` (the frames through the SDK, the server and the place in the address of the page) and `developers.html` |
| `/playground` | The playground (opens empty: the frame, a second .NET runtime, is created when a server is chosen): public test servers in one click, the editor of the configuration, theme and language, the messages, the code to copy; a second tab, the Bundle Visualizer (a Bundle pasted, opened from a file, the example `examples/bundle-example.json`, or read by its address from an open demo server) |
| `/smart/callback` | The way back of a SMART sign-in by redirect |
| `/_content/FireChart.Viewer/smart-callback.html` | The way back of a SMART sign-in in a popup |

It references the Viewer as projects and is in `FireChart.Viewer.slnx`.

## Build

From the folder of `FireChart.Viewer.slnx`: `dotnet run --project src/FireChart.Viewer.App` runs it with the development server; the Docker
image is built from the `Dockerfile` of that folder (below).

## viewer.json

```json
{
  "server": { "url": "https://fhir.example.org/r4", "name": "Our FHIR server", "fhirRelease": "R4", "pageSize": 50 },
  "smart": { "clientId": "viewer", "scopes": "launch/patient patient/*.rs openid fhirUser", "flow": "redirect" },
  "locale": "auto",
  "fhir": { "terminologyServer": "https://tx.fhir.org/r4", "pageSize": 100, "loadAll": true, "decimals": 2 },
  "appearance": { "theme": "auto", "variables": { "colorPrimary": "#0a7f5a" } }
}
```

Only `server.url` is required. `smart` is there when the server needs a sign-in (a public client with PKCE: no secret). `smart.flow` is
`redirect` (default) or `popup`; `locale` is `auto` (the language of the browser) or one of `en`, `de`, `es`, `ru`, `zh-Hans`, with a region
when wanted (`ru-RU`); `appearance.theme` is `light`, `dark` or `auto`. An unknown key, a missing one or a wrong value is shown in the page with
its path (`'apperance' is not an option. Did you mean 'appearance'?`); the Viewer does not start with a wrong configuration.

`fhir` is the FHIR settings of the Viewer (`FhirOptions`), the group FHIR settings of FireChart. `fhir.terminologyServer` turns bare codes into
words. `fhir.pageSize` is the page size of the searches of a patient summary whose summary form gives none (1 to 500, default 20; it is not
`server.pageSize`, the page of a list). With `fhir.loadAll` the summary loads every record of every section before it is shown, behind a panel
of progress with Cancel (what has come is then shown, marked incomplete), so it does not move as the sections fill. `fhir.decimals` is the
most decimal places of a value (0 to 10, default 2): a value with more is rounded, without the zeros the rounding leaves (`100.004` is `100`),
one with fewer keeps the digits of the record (`1.30`). `fhir` changes without a restart (`update`), as `locale` and `appearance` do.

## Docker

The image is nginx with the published static files, not root, port 8080: `ghcr.io/firechart/viewer:<version>` (`latest` is the newest
release, `next` the newest pre-release), or your own build of these sources (`docker compose up --build` in the folder of
`FireChart.Viewer.slnx`, or `docker build -t firechart-viewer:local .`):

```bash
docker run -d --name fhir-viewer -p 8080:8080   -v "$PWD/viewer.json:/usr/share/nginx/html/viewer.json:ro"   -v "$PWD/host.json:/usr/share/nginx/html/host.json:ro"   ghcr.io/firechart/viewer:<version>
```

`viewer.json` is the configuration (read at every load of the page), `host.json` the origins that may drive the frame (below); without them
the image has the files of the sources: the open SMART Health IT server, `'self'` only. `/` is the home page, `/playground` the playground, `/embed` the frame.

**Your own components, forms and style.** The vendor library is the folder `/usr/share/nginx/html/library` of the image (the package
`library` with its manifest `firechart.project.json`, which says its version). A project of the FireChart FHIR Designer mounted at
`/usr/share/nginx/appearance` goes over it: a file the project has wins, the others stay the vendor's and follow the updates of the image.
The project holds only the files it changes (in the same folders: `components/Patient.tile.xml`, `components/Patient.summary.xml`,
`styles/...`) and its own `firechart.project.json`.

```bash
docker run -d --name fhir-viewer -p 8080:8080   -v "$PWD/my-appearance:/usr/share/nginx/appearance:ro"   ghcr.io/firechart/viewer:<version>
```

Every file is revalidated (`no-cache`), so a changed file shows at the next load of the page without a restart. Nothing checks the project in the
container: check it in the designer (FireChart: Check Project) before you mount it. The image sends no CSP `frame-ancestors`: the proxy decides
who may frame the Viewer.

**HTTPS** comes from the reverse proxy in front of the container (nginx, Caddy, Traefik, an ingress): SMART and the browser need it. So do
the headers the image does not send: CSP `frame-ancestors` (who may frame the Viewer; without it any page may), `Cache-Control`. The image sends
one itself: `/sdk/` has `Access-Control-Allow-Origin: *` and `Cache-Control: no-cache`, as pages of other origins import the SDK. A CDN in front
(Cloudflare) must keep both: purge `/sdk/*` after an update.

**SMART.** Register the addresses of the container at the authorization server as redirect URIs: `https://<host>/smart/callback` for the
redirect and `https://<host>/_content/FireChart.Viewer/smart-callback.html` for the popup. The FHIR server must allow the origin of the
container (CORS).

## The frame and its host

A page of another application embeds the Viewer with the JavaScript SDK (`wwwroot/sdk/viewer.js`, served by the app at `sdk/viewer.js`; [Sdk/README.md](Sdk/README.md)); the playground at `/playground`
uses the same messages. The frame is opened as `embed?host=parent`, says `hello` to its parent and waits for the configuration. Envelopes
`{ protocol: "fxv", type, id?, payload }`:

| From the host | Payload | From the frame | Payload |
|---|---|---|---|
| `init` | `{ config }` | `hello` | (loaded, waits for `init`; again after the way back of a SMART redirect) |
| `restart` | `{ config }` | `ready` | `{ location, server }` |
| `update` | `{ config }`: `locale`, `appearance`, `fhir` only | `updated` | `{ applied }` |
| `navigate` | `{ location, replace? }` (`summary/Patient/123`) | `navigate` | `{ location, source: "user" \| "host" }` |
| | | `restarting`, `resize` | `resize`: `{ height }` of the content |
| | | `error` | `{ type, code, param, message }` |
| | | `result` | `{ ok, value }` or `{ ok: false, error, errors }`, for a request with an `id` |

The frame of the Bundle Visualizer (`embed-bundle?host=parent`) speaks the same envelopes. Its configuration is the keys of `viewer.json`,
`server` optional (only for a Bundle read by an address) and `smart` its client when it needs a sign-in (the sign-in comes before the address is
read; the way back of a redirect returns to the frame, never to a patient summary, and the SDK shows the last Bundle again); `update` and `restart`
are as above, and instead of `navigate`:

| From the host | Payload | From the frame | Payload |
|---|---|---|---|
| `show` | `{ bundle }` (an object, or a text of JSON or XML) or `{ location }`; answered when drawn: `{ resources, references }`, or `bundle_error` (`invalid_bundle`, `unavailable`, `cancelled`, `superseded`) | `shown` | `{ resources, references }` |
| | | `select` | `{ reference }` of the resource whose sheet opened, `null` when it closed |
| | | `ready` | `{ server }` (`null` without one) |

**Who may drive the frame:** only a parent whose origin is in `host.json` (`{ "frameAncestors": ["'self'", "https://emr.example.org"] }`), the
same syntax as CSP `frame-ancestors` (put the same list into that header of your proxy). The file of the published app allows `'self'` only;
replace it with a volume (above). `*.example.org` wildcards
and `*` work as in CSP. `hello` goes to any parent and carries nothing; the events and the results go only to the origin that sent the first
accepted message.

## Performance

The app runs on the .NET interpreter of WebAssembly; **it is not compiled with AOT, and we do not recommend turning it on** (`RunAOTCompilation=true`
stops the build with an error). Measured on 2026-11-09 (.NET 10, nginx on the same machine, the open SMART Health IT server):

| | Without AOT | With AOT |
|---|---|---|
| The published files | 71 MB | 188 MB |
| Downloaded at the first visit (Brotli; gzip from nginx) | 16 MB (12 MB) | 29 MB |
| `dotnet.native.wasm` (Brotli) | 0.9 MB | 16.3 MB |
| A publish | about 2 minutes | about 24 minutes |

- **The time goes to the FHIR server.** Without AOT the runtime starts and draws the first page in about 1 s (from the cache as from the network
  of the machine). The list of Observations then waits 3.1 s for the server and needs 0.3 s after its last answer; the Patient Summary waits
  3.5 s and needs 2.2 s after it (that part is worth profiling, not compiling; see below).
- **AOT costs on every first visit**: 13 MB more to download, 1–2 s more on a link of 50–100 Mbit/s, which is more than it could win.
- **It defeats the lazy loading** of the SDKs of the other FHIR releases (STU3, R4B, R5, R6): their code is compiled into `dotnet.native.wasm`,
  which every visitor downloads.
- **It did not work**: the AOT build stopped at the start of the runtime (`RuntimeError: null function`), with no warning of the build.

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
