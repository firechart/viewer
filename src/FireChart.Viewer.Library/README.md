# FireChart.Viewer.Library

The vendor library of FireChart Viewer: the Liquid templates of FHIR resources and datatypes, the translations of their texts, the `fx-*` style
kit built on daisyUI, the vendor search and summary forms, and synthetic samples. It is the last layer of the renderer of `FireChart.Viewer.Core`; your overrides go before it.

## Install

```bash
dotnet add package FireChart.Viewer.Library
```

## Use

```csharp
using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

// in a Blazor client: the templates are the static files of the library, loaded when the engine needs them
var renderer = new FhirRenderer([myOverrides, new WebVendorTemplates(assets)]);
var row = await renderer.RenderTileAsync(patientJson);
var css = (await FxStyle.LoadAsync(assets)).Compose(null);
var theme = await FxStyle.LoadThemeAsync(assets, "fluent"); // fx.themed.css and styles/themes/fluent.css
```

- `WebVendorTemplates` (a client) and `VendorTemplates` (a server, a test: the whole folder): the templates (`Patient@tile`, `Coding@text`, ...) and the translations (German, Spanish, French, Russian, Chinese; English is the key). `VendorLibrary` says where a file is.
- `FxStyle` and `FxStyleSheets`: the style of the visualization. `Compose(custom)` puts the vendor style in the cascade layer `fx` and a style over it in
  the later layer `fx-custom`, so it wins whatever the specificity of the selectors. `Tokens()` lists the `--fx-*` custom properties: the public
  contract of the look, declared on `:root`.
- `FxThemeSheets` and `FxStyle.LoadThemeAsync`: a theme of the Viewer, `styles/themes/{name}.css` (tokens only; `fluent` and `compact` ship), and
  `styles/fx.themed.css`, which passes the tokens to the Radzen components around the content (the Viewer composes them, `AppearanceCss`).
- `StyleChecks`: check a theme or a custom style before you use it (balanced braces, nothing loaded from outside, no scripts, the notices of the page
  not styled away).
- `VendorSearchForms`: where the search forms of the resource types of the FHIR core releases are (`VendorSearchForms.UrlOf("r4", "Patient")`); `VendorSearchFormProvider` reads one when it is first needed.
- `VendorSummaryForms`: where the summary forms are (`summary-forms/{code}.json`, read by a client when it needs one, `FindAsync`); `default` is the International Patient Summary.
- `TemplateSamples`: synthetic resources to preview and test templates (`TemplateSamples.ReadFolder`). No real patient data.

The templates, their translations, the search forms and the summary forms, the style and the samples are not in this project: they are the vendor library `public/library` (`templates`, `locales`, `search-forms`, `summary-forms`, `styles`, `samples`), a package of the designer with its manifest `firechart.project.json`. An application that sets `<FireChartLibrary>true</FireChartLibrary>` serves it as `library/...` and publishes it as the folder `wwwroot/library` (`public/Directory.Build.targets`; the build writes the version of the release into the manifest). A client loads a template when the engine first needs it (`WebVendorTemplates`), a server or a test reads them whole (`VendorTemplates`). Nothing is embedded in the assembly; `styles/daisy` holds the tools that build `public/library/styles/fx.daisy.css`.

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
