using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// What a row of a list reaches besides its own resource: the resources that came in the Bundle with it, found through a Reference, however they were fetched
public class RelatedResourcesTests
{
    private const string Patient = """{ "resourceType": "Patient", "id": "p1", "birthDate": "1974-12-25", "name": [ { "family": "Chalmers", "given": [ "Peter", "James" ] } ] }""";

    private const string EncounterR4 = """
        { "resourceType": "Encounter", "id": "e1", "status": "finished",
          "identifier": [ { "value": "V-100" }, { "value": "V-101" } ],
          "class": { "code": "AMB", "display": "ambulatory" },
          "type": [ { "coding": [ { "code": "99213", "display": "Office visit" } ] } ],
          "subject": { "reference": "Patient/p1", "display": "Peter (display)" } }
        """;

    private const string EncounterR5 = """
        { "resourceType": "Encounter", "id": "e2", "status": "completed",
          "identifier": [ { "value": "V-200" } ],
          "class": [ { "coding": [ { "code": "AMB", "display": "ambulatory" } ] } ],
          "subject": { "reference": "https://fhir.example.test/base/Patient/p1" } }
        """;

    private static readonly VendorTemplates s_vendor = new();

    private static ResourcePool PoolOf(params PoolEntry[] entries)
    {
        return FhirRenderer.CreatePool(entries);
    }

    [Test]
    public async System.Threading.Tasks.Task AnEncounterRow_ShowsThePatientAndTheIdentifiersAndTheType_FromTheResourcesThatCameWithIt()
    {
        var tile = await new FhirRenderer([s_vendor]).RenderTileAsync(EncounterR4, pool: PoolOf(new PoolEntry(EncounterR4), new PoolEntry(Patient)));

        Assert.That(tile.Text, Is.EqualTo("Peter James Chalmers, 1974-12-25 | V-100, V-101 Office visit finished"));
    }

    [Test]
    public async System.Threading.Tasks.Task WithoutThePatientInThePool_TheRowShowsWhatTheReferenceSays()
    {
        var tile = await new FhirRenderer([s_vendor]).RenderTileAsync(EncounterR4, pool: PoolOf(new PoolEntry(EncounterR4)));

        Assert.That(tile.Text, Is.EqualTo("Peter (display) | V-100, V-101 Office visit finished"));
    }

    [Test]
    public async System.Threading.Tasks.Task WithNothingToShow_TheRowShowsTheIdAndTheStatus()
    {
        const string bare = """{ "resourceType": "Encounter", "id": "e3", "status": "planned" }""";

        var tile = await new FhirRenderer([s_vendor]).RenderTileAsync(bare);

        Assert.That(tile.Text, Is.EqualTo("e3 planned"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheClassIsShownWhenThereIsNoType_InTheModelOfTheRelease()
    {
        var withoutType = EncounterR4.Replace("\"type\": [ { \"coding\": [ { \"code\": \"99213\", \"display\": \"Office visit\" } ] } ],", string.Empty, StringComparison.Ordinal);
        var models = new ReleaseModels(new NoReleaseAssemblyLoader());
        var r5 = await models.GetAsync("R5");
        var renderer = new FhirRenderer([s_vendor]);

        var r4Tile = await renderer.RenderTileAsync(withoutType, pool: PoolOf(new PoolEntry(Patient)));
        var r5Tile = await renderer.RenderTileAsync(EncounterR5, model: r5, pool: FhirRenderer.CreatePool([new PoolEntry(Patient, "https://fhir.example.test/base/Patient/p1")], r5));

        Assert.That(r4Tile.Text, Does.EndWith("| V-100, V-101 ambulatory finished"), "R4: class is one Coding");
        Assert.That(r5Tile.Text, Does.StartWith("Peter James Chalmers, 1974-12-25 | V-200 ambulatory"), "R5: class is a list of CodeableConcept, the reference is an absolute URL");
    }

    [Test]
    public void ThePool_FindsAReferenceByTypeAndId_ByAbsoluteUrl_AndWithAVersion()
    {
        var pool = PoolOf(new PoolEntry(Patient, "urn:uuid:1"));

        Assert.Multiple(() =>
        {
            Assert.That(pool.Find("Patient/p1"), Is.Not.Null);
            Assert.That(pool.Find("https://x.test/base/Patient/p1"), Is.Not.Null);
            Assert.That(pool.Find("https://x.test/base/Patient/p1/_history/3"), Is.Not.Null);
            Assert.That(pool.Find("urn:uuid:1"), Is.Not.Null);
            Assert.That(pool.Find("Patient/other"), Is.Null);
            Assert.That(pool.Find("#contained"), Is.Null);
            Assert.That(pool.Find(null), Is.Null);
        });
    }

    [Test]
    public void TheDeclarationsOfATemplate_AreReadWithoutRenderingIt()
    {
        var overrides = new MemoryTemplateSource(new Dictionary<string, string>
        {
            ["Condition@tile"] = "{% fhir_include \"Condition:subject\" %}{%- fhir_include \"Condition:encounter\" -%}{% fhir_revinclude \"Observation:focus\" %}{% tile title: \"x\" %}",
            ["Observation@tile"] = "{% tile title: \"y\" %}"
        });
        var renderer = new FhirRenderer([overrides, s_vendor]);

        Assert.Multiple(() =>
        {
            var condition = renderer.NeedsFor("Condition", "tile");
            Assert.That(condition.Include, Is.EqualTo(["Condition:subject", "Condition:encounter"]));
            Assert.That(condition.RevInclude, Is.EqualTo(["Observation:focus"]));
            Assert.That(renderer.NeedsFor("Observation", "tile").IsEmpty, Is.True, "a template that declares nothing asks for nothing");
            Assert.That(renderer.NeedsFor("Location", "tile").IsEmpty, Is.True, "no row template, no needs");
            Assert.That(renderer.NeedsFor("Encounter", "tile").Include, Is.EqualTo(["Encounter:subject"]));
            Assert.That(renderer.NeedsFor("Encounter").IsEmpty, Is.True, "the plain template is another one");
        });
    }
}
