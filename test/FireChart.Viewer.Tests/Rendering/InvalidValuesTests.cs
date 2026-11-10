using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// The data of a server is not always valid FHIR: a value that is not of its type is shown as the server sent it, the rendering does not fail
public class InvalidValuesTests
{
    [Test]
    public async Task ADateWithATime_WhereOnlyADateIsAllowed_IsShownAsItCame()
    {
        const string patient = """{ "resourceType": "Patient", "id": "p1", "birthDate": "1967-08-22T00:00:00", "name": [ { "family": "Smith", "given": [ "Ann" ] } ] }""";
        var problems = new List<string>();
        var renderer = new FhirRenderer([TestLibrary.Vendor], problems.Add);

        var tile = await renderer.RenderTileAsync(patient);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Html, Does.Contain("Smith"));
            Assert.That(tile.Html, Does.Contain("1967"));
        });
    }
}
