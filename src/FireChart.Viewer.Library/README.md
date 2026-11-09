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

var renderer = new FhirRenderer([myOverrides, new VendorTemplates()]);
var row = await renderer.RenderTileAsync(TemplateSamples.Find(TemplateSamples.Names[0])!);
```

- `VendorTemplates`: the templates (`Patient@tile`, `Coding@text`, ...) and the translations (German, Spanish, French, Russian, Chinese; English is the key).
- `FxStyle`: the style of the visualization. `FxStyle.Compose(customCss)` puts the vendor style in the cascade layer `fx` and yours in the later layer
  `fx-custom`, so yours wins whatever the specificity of the selectors. `FxStyle.Tokens()` lists the `--fx-*` custom properties to change first.
- `StyleChecks`: check a custom style before you use it (balanced braces, nothing loaded from outside, no scripts).
- `VendorSearchForms`: the search forms of the resource types of the FHIR core releases (`VendorSearchForms.Find("r4", "Patient")`).
- `VendorSummaryForms`: the clinical summary forms; `default` is the International Patient Summary.
- `TemplateSamples`: synthetic resources to preview and test templates. No real patient data.

The files are the folders of this project (`templates`, `locales`, `styles`, `search-forms`, `summary-forms`, `samples`); the assembly embeds them.

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
