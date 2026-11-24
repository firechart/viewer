# Changelog

The changes of each published release of the FireChart Viewer and the public Flox libraries, newest first.

## 0.95.4 - 2026-11-24

### Viewer

- **Components instead of templates.** Every record is drawn by the components of the library, `library/components/{Type}.{mode}.xml`
  (`text`, `link`, `tile`, `banner`, `view`, `page`, `summary`): XML of built-in blocks bound to the data by FHIRPath, checked as a whole
  before they are shown. The Liquid templates are gone.
- **Search forms are components** too, `{Type}.search.xml`, one file for every FHIR release: a filter says only what differs from the form
  the Viewer generates. The library has the forms of Patient and Encounter; any other type gets the generated form.
- **One toolbar for a record**: Record | Summary | Source, the bookmark and the menu at its end. The JSON of a record is a mode in the
  address (`?view=source`), so Back keeps it.
- **Lists** read the next page as their end comes into view (up to 500 records), show skeleton rows while it loads, and appear once, whole.
- **A summary loaded at once** shows its progress with Cancel and is about twice as fast.
- A reference to a record the server does not have is marked "could not be found"; a search the server refuses is no dead end.
- Keys: Enter applies a search form, Ctrl+F and / open the search form of a list.
- The page of the resource types chooses which types are shown (the eye of its header).
- The texts of the components are .resx resources: English, with drafts in German, Spanish, Hebrew, Russian and Chinese; the components
  read right to left in Hebrew.

### Look

- The UI is built on Blazor Blueprint (shadcn/ui tokens and Tailwind); Radzen is no longer used. A warm light theme and a dark theme.
- iOS: lists, menus and selects paint at once, typing in a field no longer zooms the page.

### Application and SDK

- The application shows a splash while the runtime downloads; the frame (`/embed`) a thin progress bar.
- Development runs on the Gateway of Blazor (`dotnet run --project src/FireChart.Viewer.App`).

### Platform (Flox)

- The SQLite of the browser: a base being imported is invisible until it is complete, and a page kept in Safari's back-forward cache gives
  its worker up and takes it back.

### Build

- .NET 11.
- The repository is laid out flat: `src/<Project>`, `test/<Project>`, `library/`, `samples/`; the Docker files are the `Dockerfile` of the
  root and `docker/`.
- GitHub builds and tests every push and pull request; a release on `main` also publishes the image `ghcr.io/firechart/viewer`
  (`linux/amd64`, `linux/arm64`) and tags the version. The version of a build is the file `VERSION`.

## 0.76.0 - 2026-11-16

### Viewer

- **Summaries by resource type.** A type with a summary form has a summary, and the page of its record switches **Record | Summary**
  (`summary/Patient/<id>`, `summary/Encounter/<id>`). The vendor library has the patient summary and a new encounter summary (diagnoses,
  procedures, results, vital signs, medication orders, orders, documents, care plans). Only the records that refer to the patient are shown,
  and a record opens in a sheet over the summary.
- **Search.** Search forms of format 2: fields and groups are hidden with `hidden`, the dialog has **Show all fields**, and a hidden field
  that has a value is always shown. Any resource type without a designed form gets one generated at run time from the metadata of its
  FHIR release. The codes of a coded field come from `ValueSet/$expand` of the FHIR server of the search.
- **FHIR searches** fall back once when a server refuses a sort or an include, have a timeout per server, and show an error tile for each
  search that failed.
- **Records:** the page of a record names references of any type; a blood pressure reads systolic / diastolic; a tile may show the code of
  its record (an ICD code of a problem) before the title; a record of a type without a tile shows the beginning of its narrative.
- **Data on the device:** bookmarks and the last search of every list are kept in SQLite (sqlite-wasm over OPFS, in a worker). The tabs and
  frames of one origin share it: one of them runs the worker and the others ask it.
- **Languages:** Hebrew, with right-to-left pages; the vendor summary forms are translated into every language of the Viewer.
- **Look:** themes Fluent and Compact (tokens only); the icons of the resource types are coloured by twelve groups (people, clinical,
  medications, diagnostics, encounters, plans, documents, devices, financial, conformance, research, infrastructure); print waits for
  everything to load.
- **Accessibility:** the section headers of a summary are headings with a button that tells whether it is expanded; record rows are links;
  units are never cut in the lab table; badges and links meet the contrast of WCAG AA; the trend chart has a text alternative; the page
  behind the record sheet is inert.
- **Security:** the identifier of a lab row is written as text, never as markup; the SMART popup answer is accepted only from the popup the
  Viewer opened.

### Templates and forms

- The template library is a folder of its own, `library/`: a project of the FireChart designer with its manifest `firechart.project.json`,
  served by the Viewer app as `library/...`.
- **Breaking:** summary forms are named by their resource type (`summary-forms/Patient.json`, `summary-forms/Encounter.json`); a summary form
  names its `context`.
- **Breaking:** search forms are of format 2 (`hidden` instead of `included`, `fhirRelease` instead of `authoredForVersion`); an older form is
  upgraded when it is read. Fields have no `options` any more. The JSON Schema of a search form is `schemas/search-form.schema.json`.
- **Breaking:** the tokens of the kit (`--fx-*`) are declared on `:root` and are the public contract of the look; themes live in
  `styles/themes/*.css` on the base `styles/fx.themed.css`. New tokens for the colours of the resource types (`--fx-type-*`).
- The vendor search forms are the designed ones of the types used most; the others are generated.
- The details, per template and per breaking change, are in `src/FireChart.Viewer.Library/CHANGELOG.md`.

### SDK

- `appearance.style` picks a theme of the library (`fluent`, `compact`, or one of the appearance folder of the installation);
  `appearance.fonts` loads fonts (`@font-face`, https only).
- A server may set `timeoutSeconds` (5 to 600, default 60).
- `locale` accepts `he`.
- **Breaking:** `clinicalSummary` is gone from the configuration and from `ViewerUpdate`: the summary of a type is its form
  (`summary-forms/<Type>.json`). Addresses of a summary are `summary/Patient/<id>`.
- `print()` loads every record of a summary first, and does not print a summary that is not complete.

### Platform (Flox)

- **Flox.Sqlite** (new): one async interface of SQLite, the documents of the device over it, a read-only `IStorage` over a SQLite file by a
  mapping, and the browser engine (sqlite-wasm in a dedicated worker over OPFS, several tabs through a leading tab).
- **Flox.Sqlite.Files** (new): the same interface over `Microsoft.Data.Sqlite`, for .NET.
- **Flox.Licensing** (new): the license model, its JSON, and the verification of a signed license (JWT, ES256).
- **Flox.Shared:** `PageHeader` (the title of a page with its commands), `JSModuleImport.ImportModuleAsync` (an import of a JavaScript module
  that the interop answers with nothing is tried again); the theme *System* follows `prefers-color-scheme`; the icon font is Material Symbols
  at weight 400 only (0.4 MB instead of 1.07 MB, every icon kept); a fire palette.
- Every public type and member of the libraries has XML documentation.

### Build

- `tools/FireChart.FhirMetadata` generates the FHIR metadata the search forms are generated from.
- Browser tests of the SQLite of the browser in several tabs (`test/Flox.Sqlite.BrowserTests`, Playwright over an installed Chrome or Edge;
  explicit).
- .NET 10.0.15, Radzen.Blazor 13.

## 0.57.0-rc.1 - 2026-11-11

Pre-release.

- The clinical summary is a stack of sections, each drawn by a component: `tiles`, `lab-results` (the results under their reports, in the order of the panel; a table in a wide section, tiles in a narrow one) and `observation-trends` (the measures of a section and a chart of the history of the chosen one). Empty sections can hide (`hideIfEmpty`).
- A toolbar on every page: the breadcrumbs, then the commands and the menu of the page.
- Bookmarks: star a page of the Viewer and find it again in the list of bookmarks. Bookmarks and recent pages are kept on the device (IndexedDB) and can be exported and imported as a file.
- Lists take the page size of the settings at once ("Records per page").
- Search forms: the fields keep away from the scrollbar; "Search all fields" is in the box on top.
- The Docker image revalidates every file (`no-cache`) and answers 404 for a missing file of the library instead of returning the page of the app.

- **Breaking:** tiles and banners are frames with slots. A tile fills `title`, `subtitle`, `trailing`, `status` (and `slot_data` for the patient, the encounter and the moment of the record) and the engine lays out the row; a banner fills `title`, `facts` and `flags` and the engine writes the header. Overrides of `@tile` and `@banner` templates must be rewritten the same way.
- **Breaking:** clinical summary forms of format 2: every section names its component and whether it hides when empty; a section can set the number of decimal places of its values (`format.decimals`). The vendor form `default` uses it (vital signs and laboratory results at two places).
- **Breaking:** the vendor library is static files, nothing is embedded in the assembly any more: templates and translations (`wwwroot/templates`, `wwwroot/locales`), clinical summary forms (`wwwroot/summary-forms`), search forms (`wwwroot/search-forms`), the style (`wwwroot/styles`) and the samples (`wwwroot/samples`). A client loads them on demand through `IViewerAssets`; the template engine loads templates asynchronously (`ITemplateSource.FindAsync`, `ITemplateTexts.PrepareAsync`).
- Numbers are rounded where a field asks for it, never padded (`{% apply ... decimals: 1 %}`); `Period@text` writes a period within one day with its date once; the encounter of a row shows its kind, with its day only when it differs from the record.
- Rows and names of the conformance resources: StructureDefinition, ValueSet, CodeSystem, SearchParameter, OperationDefinition, CapabilityStatement, ConceptMap, NamingSystem, ImplementationGuide, StructureMap.
- The search forms of StructureDefinition (R3, R4, R4B, R5) start with `kind = resource`.

See `CHANGELOG.md` of `FireChart.Viewer.Library` for the details of the API changes.

- Local storage of entities on the device (`LocalEntityStorage`, `ILocalDocumentStore`) and bookmarks that work without a server, with export and import.
- About: the version of the build is the first entry of the section.
- A slider of a form is as wide as the other fields.

## 0.51.3 - 2026-11-09

Build
- The package metadata of the Viewer projects (project and repository URL) points to this repository
- Comments and tests refer only to the projects of this repository; a test keeps it so

## 0.51.2 - 2026-11-09

First public release of the FireChart FHIR® Viewer.

Viewer
- A read-only browser of a FHIR R4 server as a client-only Blazor WebAssembly component: resource types, lists with FHIR Search,
  the page of a resource (view and source), the Clinical Summary of a patient
- SMART on FHIR sign-in and EHR launch; embedding in a frame (`/embed`) with a sandbox page at `/`
- Light and dark themes, theme variables, the locale of the host page

Templates and forms
- Liquid (Fluid) templates for the display of FHIR resources and datatypes, and the Clinical Summary forms, embedded in
  FireChart.Viewer.Library

SDK
- A dependency-free JavaScript SDK (`/sdk/viewer.js` with `viewer.d.ts`): create, mount, navigate, update, events; examples for
  plain HTML and React

Build
- `FireChart.Viewer.slnx` builds the Viewer, its libraries and tests on .NET 10
- A Docker image (nginx, non-root, port 8080) configured by `viewer.json` and `host.json`

License: MPL-2.0.
