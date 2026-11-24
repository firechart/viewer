# FireChart.Viewer.Library

The readers of the vendor library of FireChart Viewer: the components that show FHIR resources, the `fx-*` style kit, the vendor
search and summary forms, and synthetic samples. The library itself is static files; a customer's files go over it file by file.

## Install

```bash
dotnet add package FireChart.Viewer.Library
```

## Use

```csharp
using FireChart.Viewer.Library;

// in a Blazor client: the files of the library are static files, read when they are needed
var url = VendorLibrary.UrlOf("components/Patient.tile.xml");   // library/components/Patient.tile.xml?v=…
var css = (await FxStyle.LoadAsync(assets)).Compose(null);
var theme = await FxStyle.LoadThemeAsync(assets, "fluent"); // fx.themed.css and styles/themes/fluent.css
```

- `VendorLibrary`: where a host serves the library (`library/`) and the address of a file with the version of the library as its query
  (`UrlOf`); a name of a component says where its file is (`components/{Name}.xml`), there is no index.
- `FxStyle` and `FxStyleSheets`: the style of the visualization. `Compose(custom)` puts the vendor style in the cascade layer `fx` and a style over it in
  the later layer `fx-custom`, so it wins whatever the specificity of the selectors. `Tokens()` lists the `--fx-*` custom properties: the public
  contract of the look, declared on `:root`.
- `FxThemeSheets` and `FxStyle.LoadThemeAsync`: a theme of the Viewer, `styles/themes/{name}.css` (tokens only; `fluent` and `compact` ship), and
  `styles/fx.themed.css`, which passes the tokens to the Radzen components around the content (the Viewer composes them, `AppearanceCss`).
- `StyleChecks`: check a theme or a custom style before you use it (balanced braces, nothing loaded from outside, no scripts, the notices of the page
  not styled away).
- `TemplateSamples`: synthetic resources to preview and test the components (`TemplateSamples.ReadFolder`). No real patient data.

## The library

The components (the search forms and the summary forms among them), their texts, the style and the samples are not in this project: they are the
vendor library `library` (`components`, `locales`, `styles`, `samples`), a package of the designer with its manifest
`firechart.project.json`. An application that sets `<FireChartLibrary>true</FireChartLibrary>` serves it as `library/...` and publishes it as the
folder `wwwroot/library` (`Directory.Build.targets`; the build writes the version of the release into the manifest). Nothing is embedded in the
assembly.

**Components** (`components/{Name}.xml`): `{Type}.tile` (a row of a list) and `{Type}.view` (the data on the page of a record) for the common
clinical and administrative types, `{Type}.tile` for the conformance types, `Patient.banner` and `Encounter.banner`, `Patient.summary` (the sections
of the International Patient Summary) and `Encounter.summary`, and shared ones (`ListPanel`, `ObservationRow`). A type without a component of a mode
gets the built-in view of the Viewer. To change one, put a file of the same name in a designer project; to check a set of components, use
`ViewerComponentCheck` of `FireChart.Viewer.Core`. Their texts are English.

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
