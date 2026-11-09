# FireChart.Viewer.App

The application of FireChart Viewer: a Blazor WebAssembly application whose published files any web server can serve (the Docker image serves them with nginx). It shows one FHIR server, set in `viewer.json`.

| Address | What it is |
|---|---|
| `/embed` | The Viewer, the whole page: the frame another application (an EHR, a portal) embeds |
| `/` | The sandbox (opens empty: the frame, a second .NET runtime, is created when a server is chosen): public test servers in one click, the editor of the configuration, theme and language, the messages, the code to copy |
| `/smart/callback` | The way back of a SMART sign-in by redirect |
| `/_content/FireChart.Viewer/smart-callback.html` | The way back of a SMART sign-in in a popup |

It references the Viewer as projects and is in `FireChart.Viewer.slnx` (the solution of the public part) and in `FireChart.slnx`; it is the content of the Docker image `firechart-viewer` (`viewer.firechart.org`: the sandbox on).

## Build

From the folder of `FireChart.Viewer.slnx`: `dotnet run --project src/FireChart.Viewer.App` runs it with the development server,
`docker compose up --build` builds the Docker image and runs it (below). The steps of a publish are in the `Dockerfile` there.

## viewer.json

```json
{
  "server": { "url": "https://fhir.example.org/r4", "name": "Our FHIR server", "fhirRelease": "R4", "pageSize": 50 },
  "smart": { "clientId": "viewer", "scopes": "launch/patient patient/*.rs openid fhirUser", "flow": "redirect" },
  "locale": "auto",
  "terminology": { "server": "https://tx.fhir.org/r4" },
  "clinicalSummary": { "form": "default" },
  "appearance": { "theme": "auto", "variables": { "colorPrimary": "#0a7f5a" } }
}
```

Only `server.url` is required. `smart` is there when the server needs a sign-in (a public client with PKCE: no secret). `smart.flow` is
`redirect` (default) or `popup`; `locale` is `auto` (the language of the browser) or one of `en`, `de`, `es`, `ru`, `zh-Hans`, with a region
when wanted (`ru-RU`); `appearance.theme` is `light`, `dark` or `auto`. An unknown key, a missing one or a wrong value is shown in the page with
its path (`'apperance' is not an option. Did you mean 'appearance'?`); the Viewer does not start with a wrong configuration.

## Docker

The image is nginx with the static files (not root, port 8080), built from the sources: `docker compose up --build` in the folder of
`FireChart.Viewer.slnx`, or

```bash
docker build -t firechart-viewer:local .
docker run -d --name fhir-viewer -p 8080:8080 \
  -v "$PWD/viewer.json:/usr/share/nginx/html/viewer.json:ro" \
  -v "$PWD/host.json:/usr/share/nginx/html/host.json:ro" \
  firechart-viewer:local
```

`viewer.json` is the configuration (read at every load of the page), `host.json` the origins that may drive the frame (below); without them
the image has the files of the sources: the open SMART Health IT server, `'self'` only. `/` is the sandbox, `/embed` the frame.

**HTTPS** comes from the reverse proxy in front of the container (nginx, Caddy, Traefik, an ingress): SMART and the browser need it. So do
the headers the image does not send: CSP `frame-ancestors` (who may frame the Viewer; without it any page may), `Cache-Control`. The image sends
one itself: `/sdk/` has `Access-Control-Allow-Origin: *` and `Cache-Control: no-cache`, as pages of other origins import the SDK. A CDN in front
(Cloudflare) must keep both: purge `/sdk/*` after an update.

**SMART.** Register the addresses of the container at the authorization server as redirect URIs: `https://<host>/smart/callback` for the
redirect and `https://<host>/_content/FireChart.Viewer/smart-callback.html` for the popup. The FHIR server must allow the origin of the
container (CORS).

## The frame and its host

A page of another application embeds the Viewer with the JavaScript SDK (`wwwroot/sdk/viewer.js`, served by the app at `sdk/viewer.js`; [Sdk/README.md](Sdk/README.md)); the sandbox at `/`
uses the same messages. The frame is opened as `embed?host=parent`, says `hello` to its parent and waits for the configuration. Envelopes
`{ protocol: "fxv", type, id?, payload }`:

| From the host | Payload | From the frame | Payload |
|---|---|---|---|
| `init` | `{ config }` | `hello` | (loaded, waits for `init`; again after the way back of a SMART redirect) |
| `restart` | `{ config }` | `ready` | `{ location, server }` |
| `update` | `{ config }`: `locale`, `appearance`, `terminology`, `clinicalSummary` only | `updated` | `{ applied }` |
| `navigate` | `{ location, replace? }` (`summary/123`) | `navigate` | `{ location, source: "user" \| "host" }` |
| | | `restarting`, `resize` | `resize`: `{ height }` of the content |
| | | `error` | `{ type, code, param, message }` |
| | | `result` | `{ ok, value }` or `{ ok: false, error, errors }`, for a request with an `id` |

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
  of the machine). The list of Observations then waits 3.1 s for the server and needs 0.3 s after its last answer; the Clinical Summary waits
  3.5 s and needs 2.2 s after it (that part is worth profiling, not compiling; see below).
- **AOT costs on every first visit**: 13 MB more to download, 1–2 s more on a link of 50–100 Mbit/s, which is more than it could win.
- **It defeats the lazy loading** of the SDKs of the other FHIR releases (STU3, R4B, R5, R6): their code is compiled into `dotnet.native.wasm`,
  which every visitor downloads.
- **It did not work**: the AOT build stopped at the start of the runtime (`RuntimeError: null function`), with no warning of the build.

**What the profile showed** (2026-11-09; `dotnet-trace` of the tiles on the desktop runtime, timings in the browser): a row cost 1.4 ms
on the desktop and 16–70 ms in the browser, and three quarters of it was the garbage collector. Fixed: a row is parsed by AngleSharp once
(the sanitized HTML and its text in one walk, the parser kept per thread), a single value no longer asks the FHIR model again whether it is
a collection, and a resource is read from its JSON once (the pool of a page, its rows and the blocks of a summary share it). A row now costs
0.85 ms on the desktop; in the browser the main thread of the Clinical Summary is busy about a third less (2.4 s instead of 2.9 s with
the 1 s of the start), the list of Observations 17 % less. What remains: Fluid itself and AngleSharp's document per fragment.

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
