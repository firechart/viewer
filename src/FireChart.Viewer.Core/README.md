# FireChart.Viewer.Core

The engine of FireChart Viewer, plain .NET with no dependency on a server: the FHIR client per release, the engine of the **components** that show
FHIR resources (read, checked and bound to the data by FHIRPath; the Blazor blocks that draw them are in `FireChart.Viewer`), the search and summary
forms, SMART, the terminology. The resources are drawn where they are read and are never sent anywhere.

## Install

```bash
dotnet add package FireChart.Viewer.Core
dotnet add package FireChart.Viewer.Library   # the vendor components, forms and style
```

## Components

A component is an XML file `components/{Name}.xml` of a library: a tree of built-in blocks (`Section`, `Field`, `Value`, `Table`, `Tile`, `Banner`,
`Show`, `Feed`, ...) and of other components, bound to the record by FHIRPath in `{{ }}`. It holds no HTML and no code.

```xml
<Component name="Patient.tile">
  <Param name="record" required="true"/>
  <Tile title="{{iif(%record.name.exists(), %record.name.first(), %record.id)}}" subtitle="{{%record.birthDate}}">
    <Trailing>
      <Badge text="{{%record.gender}}"/>
    </Trailing>
  </Tile>
</Component>
```

A component named `{Type}.{mode}` draws a record in a mode (`tile`, `banner`, `view`, `page`, `summary`); a type without one gets a built-in view.
Before a component is shown it is checked with the components it calls: unknown blocks and attributes, variables out of scope, expressions that do
not compile, cycles. `ViewerComponentCheck` runs the same check over a set of files, for a tool or a server that installs a library:

```csharp
using FireChart.Viewer.Core.Reports;

var files = new Dictionary<string, string> { ["Patient.tile"] = File.ReadAllText("components/Patient.tile.xml") };
foreach (var problem in ViewerComponentCheck.Check(files, callsElsewhere: true))
{
    Console.WriteLine($"{problem.Component}:{problem.Line}: {problem.Message}");
}
```

`callsElsewhere: true` takes a call of a component missing from the files for one of the vendor library. R4 is the default model; STU3, R4B, R5 and
R6 are read with the Firely SDK package of that release.

## Forms

`SearchFormDefinition` is the search form of a list. The library describes it as the component `components/{Type}.search.xml`, one file for
every FHIR release: a `<SearchForm>` of `<Filter param="…">` and `<Group>`, over the form `SearchFormGenerator` makes from the metadata of the
release; `ViewerSearchForms` of `FireChart.Viewer` reads it. A filter says only what differs from the generated field:

```xml
<Component name="Encounter.search">
  <SearchForm title="{{%strings.Filter}}">
    <Filter param="subject" targets="Patient" lookup="name"/>
    <Filter param="subject:Patient.birthdate" label="{{%strings.BirthDate}}"/>
    <Group label="{{%strings.More}}" collapsed="true">
      <Filter param="class" control="select" releases="r4 r4b"/>
    </Group>
  </SearchForm>
</Component>
```

`FhirMetadata` is what the Firely SDK does not carry of a release and the search forms need: the value set each element of a resource or datatype is
bound to (with its strength), and of the search parameters their comparators, modifiers, chains and targets. It is C# generated from the core packages
(`Fhir/Metadata/*.g.cs`, STU3, R4, R4B, R5; generated, not to be edited), built when a release is first asked for.

```csharp
var r4 = FhirMetadata.Of("R4")!;
var status = r4.BindingOf("Observation.status");          // observation-status, Required
var date = r4.SearchParameterOf("Observation", "date");   // the comparators eq, gt, ge...
```

The codes of a field bound to a value set come from the FHIR server of the search: `IValueSetExpander` asks it for `ValueSet/$expand` (no
terminology server). A value set it gives whole (up to 500 codes) is filtered on the device; a larger one is searched on the server with the text
typed (`filter`), and filtered here again, since a server may ignore the filter. A server that does not expand the value set (no `$expand`, an
unknown code system, a token that may not read value sets) leaves the field a text box, and is not asked again for it.

```csharp
var expansion = await expander.ExpandAsync(server, "http://hl7.org/fhir/ValueSet/observation-status", null, null);
var finals = expansion?.Matching("fin");   // Final (final)
```

## License

MPL-2.0, see `LICENSE` and `NOTICE` of the repository. No trademark rights are granted.
