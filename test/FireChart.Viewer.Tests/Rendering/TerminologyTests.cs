using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// A code without a display is shown by the display a terminology server gave, when there is one (the pool carries the map), and by the code when there is not
public class TerminologyTests
{
    private static readonly VendorTemplates s_vendor = TestLibrary.Vendor;

    private const string Condition = """
        { "resourceType": "Condition", "id": "c1", "code": { "coding": [ { "system": "http://snomed.info/sct", "code": "38341003" } ] }, "clinicalStatus": { "coding": [ { "system": "http://terminology.hl7.org/CodeSystem/condition-clinical", "code": "active" } ] } }
        """;

    private static ResourcePool PoolWith(params (string System, string Code, string Display)[] displays)
    {
        var pool = FhirRenderer.CreatePool([]);
        pool.Terminology = new TerminologyMap();
        foreach (var (system, code, display) in displays)
        {
            pool.Terminology.Add(new CodeKey(system, code), display);
        }

        return pool;
    }

    [Test]
    public async System.Threading.Tasks.Task ACodeWithoutADisplay_IsShownByTheDisplayOfTheServer()
    {
        var pool = PoolWith(("http://snomed.info/sct", "38341003", "Hypertensive disorder"), ("http://terminology.hl7.org/CodeSystem/condition-clinical", "active", "Active"));

        var tile = await new FhirRenderer([s_vendor]).RenderTileAsync(Condition, pool: pool);

        Assert.That(tile.Text, Does.Contain("Hypertensive disorder").And.Contain("Active").And.Not.Contain("38341003"));
    }

    [Test]
    public async System.Threading.Tasks.Task WithoutTheServer_TheCodeIsShown()
    {
        var tile = await new FhirRenderer([s_vendor]).RenderTileAsync(Condition, pool: FhirRenderer.CreatePool([]));

        Assert.That(tile.Text, Does.Contain("38341003"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheOwnDisplayOfARecord_BeatsTheServer_AndTheServerBeatsTheText()
    {
        const string own = """{ "resourceType": "Condition", "id": "c1", "code": { "coding": [ { "system": "http://snomed.info/sct", "code": "38341003", "display": "High blood pressure (record)" } ], "text": "bp" } }""";
        const string typed = """{ "resourceType": "Condition", "id": "c1", "code": { "coding": [ { "system": "http://snomed.info/sct", "code": "38341003" } ], "text": "bp" } }""";
        var pool = PoolWith(("http://snomed.info/sct", "38341003", "Hypertensive disorder"));
        var renderer = new FhirRenderer([s_vendor]);

        var ownTile = await renderer.RenderTileAsync(own, pool: pool);
        var typedTile = await renderer.RenderTileAsync(typed, pool: pool);

        Assert.Multiple(() =>
        {
            Assert.That(ownTile.Text, Does.Contain("High blood pressure (record)").And.Not.Contain("Hypertensive disorder"));
            Assert.That(typedTile.Text, Does.Contain("Hypertensive disorder").And.Not.Contain("bp"));
        });
    }

    [Test]
    public void TheCodesToLookUp_AreTheCodingsWithoutADisplay_NotQuantitiesNorReferences()
    {
        const string observation = """
            { "resourceType": "Observation", "id": "o1",
              "code": { "coding": [ { "system": "http://loinc.org", "code": "718-7" }, { "system": "http://loinc.org", "code": "789-8", "display": "Erythrocytes" } ] },
              "valueQuantity": { "value": 5, "unit": "g/dL", "system": "http://unitsofmeasure.org", "code": "g/dL" },
              "interpretation": [ { "coding": [ { "system": "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation", "code": "H" } ] } ],
              "subject": { "reference": "Patient/p1" }, "identifier": [ { "system": "urn:x", "value": "1" } ] }
            """;

        var codes = TerminologyCodes.Collect([observation, "not json"]);

        Assert.That(codes, Is.EquivalentTo(new[] { new CodeKey("http://loinc.org", "718-7"), new CodeKey("http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation", "H") }));
    }
}
