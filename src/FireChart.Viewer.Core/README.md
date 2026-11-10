# FireChart.Viewer.Core

Renders FHIR resources to HTML with Liquid ([Fluid](https://github.com/sebastienros/fluid)) templates, on the client. It is the engine of
FireChart Viewer and has no dependency on a server: the resources are rendered where they are read and are never sent anywhere.

## Install

```bash
dotnet add package FireChart.Viewer.Core
dotnet add package FireChart.Viewer.Library   # the vendor templates, texts and style
```

## Render a resource

```csharp
using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

var renderer = new FhirRenderer([new VendorTemplates()], problem => Console.WriteLine($"Template: {problem}"));

// The page of a resource, with the safety net: what the templates do not show is listed, not lost
var page = FxHtml.Sanitize(await renderer.RenderViewAsync(patientJson, "view"));

// The row of a list (a daisyUI list row, sanitized already)
var row = await renderer.RenderTileAsync(patientJson);
Console.WriteLine(row.Html);
```

The resource is the JSON text a FHIR server sent. R4 is the default model; to render STU3, R4B, R5 or R6, reference the Firely SDK package of that
release and pass its `ModelInspector`.

## Layers of templates

A renderer takes layers (`ITemplateSource`); the first layer that has a key wins. Put your overrides before the vendor library:

```csharp
var overrides = new MemoryTemplateSource(new Dictionary<string, string>
{
    ["Patient@tile"] = "{% slot \"title\" %}{% apply self.name mode: \"text\" %}{% endslot %}"
});
var renderer = new FhirRenderer([overrides, new VendorTemplates()]);
```

A key is the file name of a template without extension: `Patient`, `Patient@tile`, `Coding@text`, `badge`. A layer may also add keys of its own.

## Texts

A template writes a static text as `{{ "Details" | t }}`: the key is the English text, the translations are i18next files. A layer that also implements
`ITemplateTexts` adds or overrides translations. The language is the current UI culture, then the language without the region, then English, then the key.
The engine has texts of its own (`Yes`, `No` of a boolean) in the same files.

## References between resources

A template follows a Reference with `{{ reference | resolve }}`. Give the renderer the other resources of the Bundle in a `ResourcePool`; the
references it looked for and did not find are in `ResourcePool.Misses`, so you can fetch them and render again.

```csharp
var pool = FhirRenderer.CreatePool(entries.Select(entry => new PoolEntry(entry.Json, entry.FullUrl)));
var row = await renderer.RenderTileAsync(observationJson, pool: pool);
```

## Forms

`SearchFormDefinition` and `ClinicalSummaryFormDefinition` are the search forms and the clinical summary forms; `ViewerJson` reads and writes their
files. `ViewerFormResolver` chooses the form of a key (`SearchFormKey("r4", "Patient")`, `SummaryFormKey("default")`) among providers (your server,
your configuration, the vendor providers of `FireChart.Viewer.Library`): a designed form wins over a generated one, then the provider of the lower
`Order`; a provider that fails is skipped; without any form the fallback you give makes one.

```csharp
using var resolver = new ViewerFormResolver<SummaryFormKey, ClinicalSummaryFormDefinition>(
    [myServerProvider, new VendorSummaryFormProvider()],
    (key, cancellationToken) => Task.FromResult(VendorSummaryForms.Default));
var form = await resolver.ResolveAsync(new SummaryFormKey("default"), cancellationToken);
```

## HTML is not trusted

Values of a FHIR server end up in the HTML, so pass a page through `FxHtml.Sanitize` before you place it: no scripts, no styles of its own, no images, no
forms; links only to `https` or to resources (`fhir:Type/id`, which your callback turns into an address of your application).

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
