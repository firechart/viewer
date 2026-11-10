# FireChart.Viewer.Library

The vendor library of FireChart Viewer: the Liquid templates of FHIR resources and datatypes, the translations of their texts, the `fx-*` style
kit built on daisyUI, the vendor search and clinical summary forms, and synthetic samples. It is the last layer of the renderer of `FireChart.Viewer.Core`; your overrides go before it.

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
var css = (await FxStyle.LoadAsync(assets)).Compose(customCss);
```

- `WebVendorTemplates` (a client) and `VendorTemplates` (a server, a test: the whole folder): the templates (`Patient@tile`, `Coding@text`, ...) and the translations (German, Spanish, French, Russian, Chinese; English is the key). `VendorLibrary` says where a file is.
- `FxStyle` and `FxStyleSheets`: the style of the visualization. `Compose(customCss)` puts the vendor style in the cascade layer `fx` and yours in the later layer
  `fx-custom`, so yours wins whatever the specificity of the selectors. `Tokens()` lists the `--fx-*` custom properties to change first.
- `StyleChecks`: check a custom style before you use it (balanced braces, nothing loaded from outside, no scripts).
- `VendorSearchForms`: where the search forms of the resource types of the FHIR core releases are (`VendorSearchForms.UrlOf("r4", "Patient")`); `VendorSearchFormProvider` reads one when it is first needed.
- `VendorSummaryForms`: where the clinical summary forms are (`summary-forms/{code}.json`, read by a client when it needs one, `FindAsync`); `default` is the International Patient Summary.
- `TemplateSamples`: synthetic resources to preview and test templates (`TemplateSamples.ReadFolder`). No real patient data.

The templates, their translations, the search forms and the clinical summary forms, the style and the samples are the static files of `wwwroot` (`templates`, `locales`, `search-forms`, `summary-forms`, `styles`, `samples`), served by the host as `_content/FireChart.Viewer.Library/...` (`VendorLibrary`): a client loads a template when the engine first needs it (`WebVendorTemplates`), a server or a test reads them whole (`VendorTemplates`). Nothing is embedded in the assembly; `styles/daisy` holds the tools that build `wwwroot/styles/fx.daisy.css`.

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
