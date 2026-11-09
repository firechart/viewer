# How to embed FireChart Viewer

The Viewer is a web application of its own (`FireChart.Viewer.App`) shown in an iframe. Your page needs only this SDK: one ES module, no
framework, no build step. There are no packages for React, Angular or Vue on purpose: a component of your framework is the dozen lines below,
and you keep full control over it.

Contents: [Before you start](#before-you-start) · [Plain HTML](#plain-html) · [React](#react) · [Angular](#angular) · [Vue](#vue) ·
[Recipes](#recipes) · [Troubleshooting](#troubleshooting)

## Before you start

1. **Serve the Viewer** on an origin of your own, with your page allowed to frame it:

   ```bash
   docker run -d -p 8080:8080 \
     -v "$PWD/viewer.json:/usr/share/nginx/html/viewer.json:ro" \
     -v "$PWD/host.json:/usr/share/nginx/html/host.json:ro" \
     firechart-viewer:local
   ```

   The image is built from the sources (`docker compose up --build` in the folder of `FireChart.Viewer.slnx`). `host.json` lists the origins of
   your pages, `{ "frameAncestors": ["'self'", "https://app.example.org"] }` (for a try: `http://localhost:3000`, the dev server of your
   application): the frame takes commands only from them.

2. **Take the SDK**: import it from the Viewer, `https://viewer.example.org/sdk/viewer.js` (the version of the image, CORS allowed), or copy
   `viewer.js` (and `viewer.d.ts` for TypeScript) from there into your sources; the examples below import the copy as `./viewer`.

3. **Content Security Policy of your page**, if it has one: `frame-src https://viewer.example.org`, and `script-src https://viewer.example.org`
   when the SDK is imported from the Viewer.

4. **Give the container a height.** By default the frame fills its element (`height: "fill"`); an element without a height shows nothing.
   `height: "auto"` makes the frame as high as its content instead.

5. **The FHIR server** must allow the origin of the Viewer (CORS), not the origin of your page: the requests come from the frame.

## Plain HTML

```html
<div id="viewer" style="height: 80vh"></div>
<script type="module">
  import { Viewer } from "https://app.example.org/vendor/viewer.js";

  const viewer = Viewer.create(
    { server: { url: "https://r4.smarthealthit.org" }, appearance: { theme: "auto" } },
    { url: "https://viewer.example.org/" }
  ).mount("#viewer");

  viewer.on("navigate", e => console.log("now at", e.location));
</script>
```

A complete page with buttons and a log of the events: `samples/viewer-sdk/index.html` in the repository.

## React

```tsx
import { useEffect, useRef } from "react";
import { Viewer, type ViewerConfig } from "./viewer";

type Props = {
  url: string;                                  // where the Viewer is served
  config: ViewerConfig;
  onNavigate?: (location: string) => void;
};

export function FhirViewer({ url, config, onNavigate }: Props) {
  const host = useRef<HTMLDivElement>(null);
  const viewer = useRef<Viewer | null>(null);
  const navigate = useRef(onNavigate);
  navigate.current = onNavigate;

  // One frame per address of the Viewer. The cleanup matters: StrictMode mounts twice in development
  useEffect(() => {
    const instance = Viewer.create(config, { url }).mount(host.current!);
    instance.on("navigate", e => navigate.current?.(e.location));
    viewer.current = instance;
    return () => {
      instance.destroy();
      viewer.current = null;
    };
  }, [url]); // eslint-disable-line react-hooks/exhaustive-deps

  // New props: an update (theme, language) or a restart (another server), whichever the difference needs
  useEffect(() => {
    viewer.current?.configure(config).catch(console.error);
  }, [config]);

  return <div ref={host} style={{ height: "100%" }} />;
}
```

```tsx
const [theme, setTheme] = useState<"light" | "dark">("light");
const config = useMemo<ViewerConfig>(() => ({
  server: { url: "https://r4.smarthealthit.org" },
  appearance: { theme },
}), [theme]);

<div style={{ height: 800 }}>
  <FhirViewer url="https://viewer.example.org/" config={config} onNavigate={l => console.log(l)} />
</div>
```

Keep `config` stable (`useMemo`, state): a new object on every render is compared by value and costs nothing, but a memoized one is easier
to reason about. Next.js: the component uses the DOM, so mark it `"use client"` (or load it with `dynamic(..., { ssr: false })`).

## Angular

```ts
import {
  AfterViewInit, Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, ViewChild,
} from "@angular/core";
import { Viewer, type ViewerConfig } from "./viewer";

@Component({
  selector: "fhir-viewer",
  standalone: true,
  template: `<div #host style="height: 100%"></div>`,
  styles: [`:host { display: block; height: 100%; }`],
})
export class FhirViewerComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) url!: string;
  @Input({ required: true }) config!: ViewerConfig;
  @Output() navigate = new EventEmitter<string>();
  @ViewChild("host", { static: true }) host!: ElementRef<HTMLElement>;

  private viewer?: Viewer;

  ngAfterViewInit(): void {
    this.viewer = Viewer.create(this.config, { url: this.url }).mount(this.host.nativeElement);
    this.viewer.on("navigate", e => this.navigate.emit(e.location));
  }

  // The first change comes before the view: the Viewer is created then with that configuration
  ngOnChanges(): void {
    this.viewer?.configure(this.config).catch(console.error);
  }

  ngOnDestroy(): void {
    this.viewer?.destroy();
  }
}
```

```html
<div style="height: 800px">
  <fhir-viewer url="https://viewer.example.org/" [config]="config" (navigate)="onNavigate($event)" />
</div>
```

Angular calls `ngOnChanges` when the input gets another object: replace `config` (`this.config = { ...this.config, locale: "de" }`), do not
change it in place. The events of the SDK come through `postMessage`, which zone.js patches, so change detection runs as usual.

## Vue

```vue
<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from "vue";
import { Viewer, type ViewerConfig } from "./viewer";

const props = defineProps<{ url: string; config: ViewerConfig }>();
const emit = defineEmits<{ navigate: [location: string] }>();

const host = ref<HTMLElement | null>(null);
let viewer: Viewer | null = null; // not reactive: the SDK keeps its own state

onMounted(() => {
  viewer = Viewer.create(props.config, { url: props.url }).mount(host.value!);
  viewer.on("navigate", e => emit("navigate", e.location));
});

watch(() => props.config, config => viewer?.configure(config).catch(console.error), { deep: true });

onBeforeUnmount(() => viewer?.destroy());
</script>

<template>
  <div ref="host" style="height: 100%" />
</template>
```

```vue
<div style="height: 800px">
  <FhirViewer url="https://viewer.example.org/" :config="config" @navigate="onNavigate" />
</div>
```

Do not put the `Viewer` into `ref()` or `reactive()`: Vue would wrap it in a proxy, and its private fields do not work through a proxy. Nuxt:
render it on the client only (`<ClientOnly>`). A deep `watch` sees a change made in place too; `configure` compares by value, so an unchanged
configuration does nothing.

## Recipes

**Open a patient from your application** (a button in your EMR, a row of your list):

```js
await viewer.navigate(`summary/${patientId}`);          // the clinical summary
await viewer.navigate(`resources/Patient/${patientId}`);  // the resource
await viewer.navigate("resources/Observation?code=8867-4&patient=" + patientId, { replace: true });
```

**Follow the theme and the language of your application:** give `"inherit"`; the SDK reads `data-theme` (or `data-bs-theme`, the class `dark`,
`color-scheme`) and `lang` of your `<html>` and follows their changes by itself.

```js
Viewer.create({ server: { url }, appearance: { theme: "inherit" }, locale: "inherit" }, { url: viewerUrl });
```

A language the Viewer has no texts for (`fr`) gives the language of the browser.

**Links to a patient, reloads that keep the place:** `urlParam` keeps the address of the Viewer in the address of your page.

```js
Viewer.create(config, { url: viewerUrl, urlParam: "fhir" });   // https://app.example.org/chart?fhir=summary/123
```

The SDK only replaces the parameter (`history.replaceState`): the moves inside the Viewer are in the history of the tab already, so Back works
as usual. A router of your application that rewrites the query may drop the parameter: let it keep unknown parameters.

**Print:** `await viewer.print()` opens the print dialog for the page of the Viewer only.

**Your colours and shapes:**

```js
viewer.update({ appearance: { variables: { colorPrimary: "#0a7f5a", borderRadius: "6px" } } });
viewer.update({ appearance: { rules: { ".Banner": { backgroundColor: "#f1f5f9" } } } });
```

The variables: `colorPrimary`, `colorBackground`, `colorSurface`, `colorText`, `colorTextSecondary`, `colorBorder`, `colorDanger`,
`colorWarning`, `colorSuccess`, `colorInfo`, `fontFamily`, `borderRadius`. The selectors of the rules: `.Banner`, `.Tile`, `.Badge`, `.Card`,
`.Table`, with colours, borders, radius, shadow, font, spacing and the like. A value the Viewer cannot use is left out with an `error` event.

**Handle refusals:**

```js
try {
  await viewer.navigate(userInput);
} catch (error) {
  if (error.code === "invalid_location") showMessage("Not an address of the viewer");
  else throw error;
}
```

**Another server:** `viewer.restart({ server: { url } })`, or `configure` with the new configuration. Sign-ins are kept per server, so going back
does not ask again within the tab.

**SMART sign-in in a frame:** prefer `smart: { flow: "popup" }`: many authorization servers refuse to be shown in a frame. Register both
redirect URIs of the Viewer at the authorization server: `https://viewer.example.org/smart/callback` and
`https://viewer.example.org/_content/FireChart.Viewer/smart-callback.html`.

## Troubleshooting

| Symptom | Cause | What to do |
|---|---|---|
| The frame is empty; the console says *Refused to frame ... frame-ancestors* | Your proxy sends CSP `frame-ancestors` without the origin of your page | Add it there |
| The frame shows the Viewer of `viewer.json`, not your configuration, and no event comes | The frame does not obey an origin that is not in its `host.json` | Add it to `host.json`, reload |
| Nothing is visible at all | The element has no height (`height: "fill"`) | Give the element a height, or `height: "auto"` |
| Two frames in development (React) | StrictMode mounts twice, the cleanup does not destroy | Return `() => viewer.destroy()` from the effect |
| *Failed to fetch* inside the Viewer | The FHIR server does not allow the origin of the Viewer (CORS) | Allow `https://viewer.example.org` on the FHIR server |
| `update` rejects with `restart_required` | `server` or `smart` changed | `restart(config)`, or `configure(config)` |
| The Viewer has the old version after an upgrade | A cached script | The image sends `no-cache`; a proxy in between may cache: let it pass the headers |
