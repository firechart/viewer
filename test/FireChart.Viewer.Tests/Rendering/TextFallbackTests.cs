using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// What a person reads is made of the coded and the structured parts first; the free text of a concept, of a name or of an address is the fallback (it is optional and may disagree with the codes)
public class TextFallbackTests
{
    private static readonly VendorTemplates s_vendor = TestLibrary.Vendor;

    private static async System.Threading.Tasks.Task<string> TileTextAsync(string json)
    {
        return (await new FhirRenderer([s_vendor], null, FixedClock.Offset(0)).RenderTileAsync(json)).Text;
    }

    private static string Condition(string code)
    {
        return $$"""{ "resourceType": "Condition", "id": "c1", "code": {{code}} }""";
    }

    [Test]
    public async System.Threading.Tasks.Task AConcept_IsShownByTheDisplayOfItsCoding_NotByItsText()
    {
        var text = await TileTextAsync(Condition("""{ "coding": [ { "system": "s", "code": "38341003" }, { "system": "s", "code": "x", "display": "Hypertension" } ], "text": "my blood pressure" }"""));

        Assert.That(text, Does.Contain("Hypertension").And.Not.Contain("my blood pressure"));
    }

    [Test]
    public async System.Threading.Tasks.Task AConceptWithoutADisplay_FallsBackToItsText_ThenToTheBareCode()
    {
        var fromText = await TileTextAsync(Condition("""{ "coding": [ { "system": "s", "code": "38341003" } ], "text": "High blood pressure" }"""));
        var fromCode = await TileTextAsync(Condition("""{ "coding": [ { "system": "s", "code": "38341003" } ] }"""));

        Assert.Multiple(() =>
        {
            Assert.That(fromText, Does.Contain("High blood pressure").And.Not.Contain("38341003"));
            Assert.That(fromCode, Does.StartWith("38341003"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task AName_IsMadeOfItsParts_AndItsTextIsTheFallback()
    {
        var parts = await TileTextAsync("""{ "resourceType": "Patient", "id": "p1", "name": [ { "text": "J. Doe", "family": "Doe", "given": [ "John" ] } ] }""");
        var onlyText = await TileTextAsync("""{ "resourceType": "Patient", "id": "p2", "name": [ { "text": "J. Doe" } ] }""");

        Assert.Multiple(() =>
        {
            Assert.That(parts, Does.Contain("John Doe").And.Not.Contain("J. Doe"));
            Assert.That(onlyText, Does.Contain("J. Doe"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task AnAddress_IsMadeOfItsParts_AndItsTextIsTheFallback()
    {
        var parts = await TileTextAsync("""{ "resourceType": "Organization", "id": "o1", "name": "Acme", "address": [ { "text": "Somewhere", "city": "Berlin" } ] }""");

        Assert.That(parts, Does.Contain("Berlin"));
    }
}
