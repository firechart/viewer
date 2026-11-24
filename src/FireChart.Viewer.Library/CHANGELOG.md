# Changelog

The history of the template library. It has the version of the solution (the release tag) and ships embedded in FireChart.Viewer.Library;
the notes of a release are the section of that version. Patch: a fix of a text. Minor: new templates. Major: a key removed or renamed, or a new engine contract.

## Unreleased

- **Major:** the kit has no daisyUI: `styles/fx.daisy.css` and the tools of `styles/daisy` are gone, the components of the kit (badge, card, list,
  table, stat, status, skeleton, kbd, divider, alert) are written in `fx.css` in the layer `kit` with the same class names, and
  `FxStyleSheets` holds `fx.css` alone. The families the library never used (`fx-collapse`, `fx-timeline`, `fx-join`, `fx-avatar`, the stack of
  cards) are gone. Without a theme the tokens follow the theme of Blazor Blueprint of the host, and `fx.themed.css` writes its tokens.
- **The search forms are components** (major: a new engine contract): `components/{Type}.search.xml`, one file for every FHIR release, a
  `<SearchForm>` of `<Filter param>` and `<Group>` over the form the Viewer generates; the folder `search-forms` (180 JSON files) and
  `schemas/search-form.schema.json` are gone. The library has `Patient.search`; its texts are keys of `locales/strings.resx` (`Filter`, `FamilyName`,
  `GivenName` added in every language).
- Accessibility of the kit (patch, the classes and the slots are the same): a field (partial `field`) is a `dl` of its `dt` (`fx-label`) and `dd`
  (`fx-value`), so a reader links the label to the value; a banner is a `div class="fx-banner"` (the frame and the partial `view-header`), no more a
  `header`, which made two or three banner landmarks of a page; a tile row is a `div class="fx-list-row"` (the frame; the list of the rows is the
  container). `fx.css`: the text of a soft badge is its tone mixed toward the text (4.5:1), a link in running text is underlined, a value and a
  range of the lab table wrap instead of losing the unit, the layout of a field follows the width of the view (container `fx-view`), the rows grow
  with the user's line height (`lh`), forced colours and `prefers-contrast: more` are handled, hover only where there is one; the muted colour of
  the vendor palette is darker (4.5:1 on the banner), `--color-warning-content` is `--fx-color-on-warning`, Fluent's text on warning is dark.

- **Major:** a summary form is named by the type it grows from: `summary-forms/Patient.json` (was `default.json`) and `summary-forms/Encounter.json`
  (was `encounter.json`); its `code` is the type. A type with a form has a summary, and the page of its record switches to it (**Record | Summary**).
  There is no default form any more, and a FHIR server does not choose a form (`summary.form` of the configuration is gone).
- The search dialog has **Show all fields** when the form hides some: the hidden fields and groups appear in their places. A hidden field
  that has a value (a default value, a parameter of the address) is always shown, so nothing that narrows the list is out of sight; the list shows
  it as a chip too.
- **Major:** the search forms are of format 2 (`formatVersion`): `hidden` instead of `included`, `fhirRelease` (`R4`) instead of
  `authoredForVersion` (`4.0.1`, a form of the core resources serves every package on them). A form of format 1 is upgraded when it is read; the
  Viewer reads a search form laxly (`SearchFormReader`: an unknown property ignored, an unknown value replaced by the default, a field it cannot
  read left out, each with a warning in the log). The engine needs `SearchFormReader`.
- The metadata of R4 and R5 are generated from the current builds of their core packages (the registry republished them): the bindings of the
  units of `Age`, `Distance` and `Duration` are new. The generator is public: `src/FireChart.FhirMetadata`.
- `schemas/search-form.schema.json`: the JSON Schema of a search form, made from the model of the Viewer (strict: no other property).
- **Major:** a field of a search form is hidden with `hidden: true` (optional, `false` when missing) instead of `included: false`, and a group
  can be hidden too (its fields with it); a hidden field still sends its value (a default value is a fixed filter). The search dialog has no
  "More filters" section any more: a hidden field is not shown at all. A form with `included` is refused by the Viewer; the vendor forms are rewritten.
  The engine needs `SearchFormField.Hidden`, `SearchFormGroup.Hidden`.
- **Major:** the vendor search forms are the designed ones of the types used most (Patient, Encounter, Organization, Practitioner, the clinical types
  and the conformance resources: 180 files instead of 531); the form of any other type is generated at run time from the metadata of the release
  (`SearchFormGenerator`; the engine needs it and `FhirMetadata.ResourceTypes`, `ParametersOf`).
- **Major:** the fields of the search forms have no `options` any more (`SearchFormField.Options`, `SearchFormOption` removed; the key is gone from
  every file of `search-forms`, where it was always empty). A coded field names its value set (`valueSetUrl`) and the Viewer asks the FHIR server of
  the search for its codes (`ValueSet/$expand`); a form with `options` is refused by the Viewer.
- **Major:** the tokens of the kit (`--fx-*`) are declared on `:root`, not on `.fx-root` (a style that set them on `.fx-root` still works there), and
  are the public contract of the look; new tokens `--fx-font-mono`, `--fx-color-background`, `--fx-color-neutral`, `--fx-color-on-accent`,
  `--fx-color-link`, `--fx-color-on-warning`. Themes of the Viewer: `styles/themes/fluent.css` and `compact.css` (tokens only) and
  `styles/fx.themed.css`, the base of a theme (the vendor palette as values and the bridge from the tokens to the Radzen variables). The engine
  needs `FxStyle.LoadThemeAsync`, `FxThemeSheets`. The customer style of the server (`fx.custom.css` of FireChart, edited at FireChart > FHIR Style)
  is gone: a theme is a file of the appearance folder.
- Print: `@media print` in `fx.css` (black on white, no break inside a banner, a row or a card, no buttons) and `fx-print-incomplete`, the note on paper
  of a page that is not complete.
- Hebrew: `locales/he/fhir-templates.json` (plurals one, two, other) and the texts of the vendor summary forms. The styles of the kit say start and
  end (`margin-inline-start`, `inset-inline-end`...), so a right-to-left page turns them. The age of a patient is an engine text in every language
  (`{{count}} y`, `{{count}} mo`, `{{count}} d`); the engine needs it.
- The vendor summary forms have their titles and descriptions in every language of the product (a test keeps every text of them so).
- New vendor form `summary-forms/encounter.json`: the summary of an encounter (diagnoses, procedures, results, vital signs, medication orders, orders,
  documents, care plans). Summary forms are of format 3: the form names its `context` (`Patient`, `Encounter`), a search its `contextParam`
  (`patientParam` before, still read). The engine needs `SummaryFormDefinition.Context`.
- Renamed, nothing in the templates changed: the "clinical summary" is the patient summary (`SummaryFormDefinition`, `summary/Patient/<id>`, the
  configuration key `summary` instead of `clinicalSummary`, the entity `SummaryForm`; the older address `summary/<id>` still opens).
- The value of a blood pressure reads systolic / diastolic (LOINC 8480-6 first) in a tile and in `lab-results`, whatever the order of the components of the
  record; the new partial `observation-values` joins the values.
- The page of a record names the resources of any type it refers to (the reason of a prescription was a bare `Condition/…`): the engine fetches the
  references the page did not get, not only patients and encounters (`IReferenceFetcher.FetchAsync(anyType: true)`, at most 50).
- A tile may show the code of its record before the title, in the systems a section of the clinical summary names (`options.codeSystems`, in the order of
  preference): the vendor form shows the ICD code (ICD-10, ICD-10-CM, ICD-11, ICD-9-CM) of a problem. The tiles of Condition, Procedure, Observation,
  DiagnosticReport, AllergyIntolerance and Immunization name their codings with `{% slot_data codes: self.code %}`; the class `fx-tile-code`. The engine
  needs `slot_data codes` and `TileOptions.CodeSystems`; nothing changes without the option.
- First library: Patient, Patient.contact, HumanName, the generic primitive and complex views, the row partial.
- The page of a resource (mode `view`) on the classes of the kit fx-*: Patient, Encounter, Observation, Procedure, Condition. The resources other than Patient
  start with the banners of the patient and the encounter (mode `banner`, partial `view-header`), found through the references of the resource.
- Text and link modes of the datatypes and of the resources a reference leads to (`@text`, `@link`).
- The filters `as_list`, `age`, `fhir_date`, `fhir_href`: the templates need an engine that has them.
- Tiles (mode `tile`) of Condition, Procedure and Observation: the name of the code, and the state (status, value) with the moment as the subtitle.
- `Period@text`: an empty start or end is shown as `…` (`… - 2022-01-02`, `2022-01-01 - …`), both empty shows nothing. The tile of an Observation joins the values of its components with " / ".
- Dates, decimals and booleans are written in the culture of the application (`fhir_date`, `fhir_number`, `fhir_bool`; `date@text`, `decimal@text`, `boolean@text`, the birth date of the patient templates, `Quantity@text`): `01.01.2022` in `ru-RU`. The engine that renders the library must have the new filters and take `CultureInfo.CurrentCulture`.
- Tiles (mode `tile`) may have inline HTML in the title and the subtitle (badges of the kit); the engine encodes the values of the record and lets through inline elements only, no links. The engine needs `FxHtml.SanitizeInline`; the style `.fx-inline` was added. A template that is broken shows its error on the page and in the row.
- The look of the page of the clinical summary is in `fx.css` (classes `fx-summary-*`, tokens `--fx-color-divider`, `--fx-color-group`, `--fx-summary-inset`), so `fx.custom.css` restyles it: the card of the patient, the grouped list, the headers of the blocks, the rows. The height of a row is the token `--fx-tile-height` (3.5rem; `--fx-tile-height-narrow`, 4.75rem, when the list is under 26rem wide): the list of the page measures it.
- The kit has a subset of daisyUI 5 (classes `fx-badge`, `fx-card`, `fx-list`, `fx-table`, `fx-stat`...; styles/daisy, built by build.cmd), coloured by the `--fx-color-*` tokens. The badge partial writes `fx-badge fx-badge-soft fx-badge-sm fx-badge-<tone>`; `fx-badge-danger` stays as the older name of `fx-badge-error`. The engine that composes the style needs `FxStyle.Daisy` (embedded `fx.daisy.css`).
- **Major:** the mode `tile` writes HTML, a daisyUI list row (`<li class="fx-list-row">`), instead of the JSON of the tag `{% tile %}` (the tag is gone). The vendor `Patient`, `Encounter`, `Condition`, `Procedure` and `Observation` tiles use the new partial `tile-row` and show the age, the sex, the status and the value as badges. New classes `fx-tile-title`, `fx-tile-subtitle`, `fx-tile-value`, `fx-tile-error`. An override of a `@tile` template must be rewritten.
- Rows (`@tile`), names (`@text`) and pages (`@view`) for the most used types of R4: Practitioner, PractitionerRole, Organization, Location, RelatedPerson, Device, EpisodeOfCare, Appointment, AllergyIntolerance, MedicationRequest, MedicationStatement, Immunization, DiagnosticReport, Specimen, ImagingStudy, CarePlan, Goal, CareTeam, ServiceRequest, Task, Communication, DocumentReference, Consent, Contract, Account, Coverage, CoverageEligibilityRequest, CoverageEligibilityResponse, Claim, ExplanationOfBenefit. The datatype `Money@text`; the badge partial knows more codes (draft, requested, booked, permit, deny, stat, high...). `fx-badge-danger` uses the variables of the daisyUI badge (it had no colour before).
- A tile adapts to a narrow list like a row of the application: the partial `tile-row` writes `fx-tile` with `fx-tile-main` (title, subtitle) and `fx-tile-end` (only when there is a trailing part). In a list under 26rem the end is the third line (the rows are taller), on a wide one it stays on the right and takes at most half of the row; the lines and a value are cut with an ellipsis, the badges never. The engine's fallback row (`TileModel`) writes the same classes; an override of `@tile` that writes its own `<li>` keeps the old grid of daisyUI.
- The partial `parts`: the parts of a line that are not blank, joined by " - " (p1 to p5). The vendor tiles use it for the subtitle and take the title through one chain of `default`; no text changes.
- **Behaviour:** the free `text` of a CodeableConcept, of a HumanName and of an Address is the fallback now: the display of a coding (a name: given and family, an address: its parts) comes first, then the text, then the bare code. A record that had both may read differently.
- **Major:** five modes (`text`, `link`, `tile`, `banner`, `view`); the rows of the clinical summary are tiles. `Condition@row`, `Encounter@row`, `Observation@row`, `Procedure@row`, `_complex@row`, `_list@summary` and the partial `row-link` are gone. The partial `tile-row` takes `status`, `patient`, `encounter` and `at`: it names the patient (partial `tile-patient`) and the encounter (partial `tile-encounter`) on the second line unless the page shows them above, adds the age at the event and shows the status as the options say. The engine must set `fhir_context` and `fhir_options` and have the filter `in_context`. Every vendor tile passes its status, patient, encounter and moment; `Procedure@tile` is titled by its code (the patient was the title); `Encounter@tile` in a list of its own is titled by the patient and the number of the encounter (`Name, birth date | number`) with the kind and the period below, and by its kind below the banner of its patient. The partial `parts` takes `sep`. An override of a `@tile` template should call `tile-row` the same way.
- `DeviceUseStatement@text`, `@tile`, `@view` (the medical devices section of the International Patient Summary).
- The static texts are translated (filter `t`, `locales/{de,en,es,fr,ru,zh}/fhir-templates.json` in the format of i18next with plural forms); the partials `field`, `row` and `badge` translate their label and text. Counts take the plural form of the language ("1 participant", not "1 participants"). The engine must have the filter `t` and read the locale files.
- A reference to a patient or an encounter the server does not have shows a warning badge ("Patient could not be found", "Encounter could not be loaded"): `tile-patient`, `tile-encounter`, `view-header`, `Encounter@tile`. The engine must have the filter `missing`.
- Rows (`@tile`) and names (`@text`) of the conformance resources: StructureDefinition, ValueSet, CodeSystem, SearchParameter, OperationDefinition, CapabilityStatement, ConceptMap, NamingSystem, ImplementationGuide, StructureMap. Built from their summary elements only (the lists of the Conformance Browser ask for `_summary=true`), the name first, the facts joined by " · ". New texts `abstract` and `{{count}} concepts`.
- **Major:** the templates and the translations are static files (`wwwroot/templates`, `wwwroot/locales`), no longer embedded: `VendorTemplates` reads a folder or a file provider (`VendorTemplates.FromFolder`), a client loads them on demand (`WebVendorTemplates` over `IViewerAssets`). The engine must load templates asynchronously (`ITemplateSource.FindAsync`, `ITemplateTexts.PrepareAsync`) and load the partials a template renders with it. A key says where its file is (`VendorLibrary.TemplatePathOf`): `datatypes` for the datatypes and primitive types, `resources` for the rest that starts with a capital letter, `partials` for the others, the root for `_complex` and `_primitive`.
- **Major:** the clinical summary forms are static files (`wwwroot/summary-forms`): `VendorSummaryForms.FindAsync` reads one through `IViewerAssets`, `Read` and `ReadFolder` read them all; `Codes`, `Find`, `TextOf` and `Default` are gone, `VendorSummaryFormProvider` takes the assets.
- **Major:** the style is static files (`wwwroot/styles/fx.css`, `fx.daisy.css`): `FxStyleSheets` holds them (`Compose`, `Tokens`), `FxStyle.LoadAsync` reads them through `IViewerAssets`, `Read` and `ReadFolder` through a file provider or a folder; `FxStyle.Vendor`, `Daisy`, `Compose` and `Tokens` are gone. `styles/daisy/build.cmd` writes into `wwwroot/styles`.
- **Major:** the samples are static files (`wwwroot/samples`): `TemplateSamples` is read through a file provider or a folder (`Read`, `ReadFolder`) and is an instance. Nothing of the library is embedded in the assembly any more.
- The search forms of StructureDefinition (r3, r4, r4b, r5) start with `kind = resource`: a list of the Viewer opened for the first time is searched by the default values of its form.
- **Major:** a tile (mode `tile`) fills slots instead of writing the row: `{% slot "title" %}`, `subtitle`, `trailing`, `status` (blocks), and `{% slot_data patient: …, encounter: …, at: … %}` for the references of the record; what is written outside the slots is dropped, a tile without a title shows the id and the type. The engine lays the row out (the second line with the age, the patient and the encounter, the status by the options of the list), so the partial `tile-row` is gone; `tile-patient`, `tile-encounter` and `age-at` stay and are rendered by the tag. Every vendor `@tile` was rewritten; an override of a `@tile` template must be rewritten the same way. The library needs an engine with the tags `slot` and `slot_data`.
- The encounter of a row (partial `tile-encounter`, given `at`) is its kind, with its day only when the record is of another day or has no moment of its own; the number and the period of the visit are no longer repeated on every row. The engine needs the filter `fhir_day` and `slot_data` passing `at` to the partial.
- A number is rounded where a field asks for it (at most that many places, never padded: `1.3` stays `1.3`): `{% apply self.value mode: "text", decimals: 1 %}` (`Quantity@text`, `decimal@text` call `fhir_number: decimals`); nothing asked keeps the digits of the record. The engine needs `fhir_number` with the argument and `apply` passing its named arguments to the templates.
- `Period@text` writes a period of one day with the date once (`2022-01-01 10:00 - 11:30`) and a period of one date as that date; a period across days, or with one end without a time zone, keeps both ends. The engine needs the filters `fhir_day` and `fhir_time`.
- `Quantity@text` marks its number and unit (`fx-quantity-number`, `fx-quantity-unit`); in a tile the digits are cut with an ellipsis and the unit is always whole (`fx.css`).
- `Observation@tile` rounds its value and the values of its components by the format of the section (`fhir_options.decimals`, the `Format` of a section of the clinical summary form); none keeps the digits of the record. The engine needs `fhir_options.decimals` from `TileContext.Format`.
- The vendor summary form `default`: the vital signs show at most two decimal places (`format.decimals: 2`).
- **Major:** a banner (mode `banner`) fills slots `title`, `facts` (each a `<span>`) and `flags` (badges); the engine writes the `<header class="fx-banner">` (a sub-banner for any resource but a patient), its blocks and the link of the title when `banner_link` is set. `Patient@banner` and `Encounter@banner` rewritten; an override must be rewritten the same way. The engine needs the banner frame.
- The vendor summary form `default` is of format 2: every section names its component (`tiles`) and whether it hides when empty (`hideIfEmpty`: the empty sections are dropped but the three required ones of IPS, medications, allergies and problems). The engine needs `ClinicalSummaryFormDefinition` of format 2.
- The partial `lab-row`: the cells of a row of the laboratory results (slots `test`, `value`, `range`, `flag`) for the component `lab-results`; the vendor summary form shows the diagnostic results with it, rounded to two places. The engine needs `RenderSlotsAsync` and the component.
- The vendor summary form shows the vital signs with the component `observation-trends` (the measures and the chart of the history of the chosen one).
- The engine shows a record of a type without a `@tile` (Basic and the other rare types) as the beginning of its narrative with the type below, else its id and type: no template is needed for such types.
- **Major (the delivery, no template changes):** the library is the folder `library`, a package of the designer with its manifest `firechart.project.json` (name `firechart-vendor`, version of the release, `engine` 1), served as `library/...` instead of `_content/FireChart.Viewer.Library/...`; the project `FireChart.Viewer.Library` holds only the readers. A host that read the old address must read `VendorLibrary.ContentRoot`.
- The style of the error tile of a failed search of a section of the clinical summary: `fx-summary-error` (with `-icon`, `-text`, `-title`, `-detail`) and the sign in the header `fx-summary-failed`; `fx.custom.css` may restyle them.
