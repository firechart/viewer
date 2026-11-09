# FireChart Viewer SDK

FireChart Viewer in any web page: resource types, lists, resources and the clinical summary of a patient, straight from a FHIR server. The
Viewer runs in an iframe of its own origin (`FireChart.Viewer.App`, served by you with its Docker image, or by us); this SDK creates the frame,
gives it the configuration and turns its messages into events and promises. One ES module, no dependencies, no build.

The image of the Viewer serves the SDK of its own version at `sdk/viewer.js` (and `sdk/viewer.d.ts`), so the SDK and the frame always
speak the same protocol: import it from there, or copy both files into your sources. The source is `wwwroot/sdk` of `FireChart.Viewer.App`.

```html
<div id="chart" style="height: 800px"></div>
<script type="module">
  import { Viewer } from "https://viewer.example.org/sdk/viewer.js";

  const viewer = Viewer.create(
    { server: { url: "https://r4.smarthealthit.org" }, appearance: { theme: "auto" } },
    { url: "https://viewer.example.org/" });
  viewer.on("navigate", e => console.log(e.location));
  viewer.mount("#chart");

  await viewer.navigate("summary/13a00221-8417-4973-961e-e21b32ece95a");
  await viewer.update({ locale: "de", appearance: { theme: "dark" } });
</script>
```

**React, Angular, Vue:** there are no packages for them on purpose; [HOWTO.md](HOWTO.md) has a component of a dozen lines for each, plain
HTML, recipes and troubleshooting.

## Where the Viewer is served

`options.url` is the address of `FireChart.Viewer.App`. The frame takes commands only from the pages that may frame it: the origin of your
page must be in its `host.json`, `{ "frameAncestors": ["'self'", "https://emr.example.org"] }`, mounted into the Docker image (built with
`docker compose up --build`):

```bash
docker run -d -p 8080:8080 -v "$PWD/host.json:/usr/share/nginx/html/host.json:ro" firechart-viewer:local
```

## API

| Member | What it does |
|---|---|
| `Viewer.create(config, options)` | A Viewer; `config` is the configuration (the keys of `viewer.json`), `options` `{ url, height?, title? }` |
| `mount(target)` | Puts the frame into an element or the first element of a selector |
| `on(event, handler)` / `off` | `ready`, `navigate`, `restarting`, `updated`, `error`; `on` returns the function that unsubscribes |
| `update(changes)` | Changes `locale`, `appearance`, `terminology`, `clinicalSummary` without a restart; resolves with `{ applied }` |
| `restart(config)` | Starts anew with another configuration (another server, another SMART client) |
| `configure(config)` | Restarts or updates, whichever the difference from the configuration in effect needs: for the props of a component (see `HOWTO.md`) |
| `print()` | The print dialog of the browser for the page of the Viewer |
| `navigate(location, { replace })` | Goes to an address of the Viewer: `resources/Patient`, `resources/Patient/123`, `summary/123`, `resources/Observation?code=8867-4` |
| `location`, `options` | The address of the Viewer and the configuration in effect |
| `destroy()` | Removes the frame |

`height: "fill"` (default) fills the element: give it a height. `height: "auto"` makes the frame as high as its content.

`appearance.theme: "inherit"` and `locale: "inherit"` take the theme and the language of your page (`data-theme`, `data-bs-theme`, the class
`dark`, `color-scheme`; `lang` of `<html>`) and follow their changes. `urlParam: "fhir"` keeps the address of the Viewer in the address of your
page (`?fhir=summary/123`), so a link or a reload opens the Viewer where it was.

A request the Viewer refuses rejects its promise with a `ViewerError` (`type`, `code`, `param`, `message`): `update({ server })` is
`restart_required`, `navigate("https://...")` is `invalid_location`, a wrong key is `unknown_option` with a hint (`Did you mean 'appearance'?`).
A problem the Viewer meets by itself (a value of the appearance it cannot use) is an `error` event; the Viewer goes on without that value.

## SMART sign-in

Register the addresses of the Viewer as redirect URIs: `<url>smart/callback` (redirect) and
`<url>_content/FireChart.Viewer/smart-callback.html` (popup). In a frame, `smart.flow: "popup"` is the safer choice: many authorization
servers refuse to be framed. After a redirect the frame loads again and the SDK gives it the configuration again by itself.

## License

MPL-2.0. No trademark rights are granted.
