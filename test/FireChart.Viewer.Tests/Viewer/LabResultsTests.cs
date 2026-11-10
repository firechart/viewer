using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Library;
using FireChart.Viewer.Pages;

namespace FireChart.Viewer.Tests.Viewer;

// The component lab-results: the results under the reports that list them, in the order of the report; the rest by day; the cells of a row
public class LabResultsTests
{
    private static ResourceSummary Obs(string id, string effective)
    {
        return new ResourceSummary(id, "Observation", $$"""{ "resourceType": "Observation", "id": "{{id}}", "status": "final", "code": { "text": "{{id}}" }, "effectiveDateTime": "{{effective}}" }""");
    }

    private static ResourceSummary Report(string id, string effective, params string[] results)
    {
        var list = string.Join(", ", results.Select(result => $$"""{ "reference": "{{result}}" }"""));
        return new ResourceSummary(id, "DiagnosticReport", $$"""{ "resourceType": "DiagnosticReport", "id": "{{id}}", "status": "final", "effectiveDateTime": "{{effective}}", "result": [ {{list}} ] }""");
    }

    [Test]
    public void TheResults_AreUnderTheirReportInItsOrder_TheRestByDay_NewestFirst()
    {
        var items = new[]
        {
            Obs("k", "2020-03-02T09:00:00Z"),
            Report("panel", "2020-03-01T08:00:00Z", "Observation/na", "Observation/k-old", "https://x.example/fhir/Observation/cl"),
            Obs("na", "2020-03-01T08:00:00Z"),
            Obs("lone", "2020-02-01T10:00:00Z"),
            Obs("lone2", "2020-02-01T12:00:00Z")
        };
        var included = new[] { Obs("k-old", "2020-03-01T08:00:00Z"), Obs("cl", "2020-03-01T08:00:00Z") };

        var groups = LabResults.Group(items, included, TimeZoneInfo.Utc);

        Assert.That(groups.Select(group => $"{group.Key}: {string.Join(",", group.Rows.Select(row => row.Id))}"), Is.EqualTo(new[]
        {
            "day:2020-03-02: k",
            "DiagnosticReport/panel: na,k-old,cl",
            "day:2020-02-01: lone,lone2"
        }));
    }

    [Test]
    public async System.Threading.Tasks.Task TheCellsOfARow_AreTheTestTheValueOfTheFormatTheRangeAndTheFlag()
    {
        const string json = """
            { "resourceType": "Observation", "id": "o", "status": "final", "code": { "text": "Potassium" },
              "valueQuantity": { "value": 4.51196, "unit": "mmol/L" }, "interpretation": [ { "coding": [ { "code": "H", "display": "High" } ] } ],
              "referenceRange": [ { "low": { "value": 3.5, "unit": "mmol/L" }, "high": { "value": 5.1, "unit": "mmol/L" } } ] }
            """;

        var cells = await new FhirRenderer([TestLibrary.Vendor]).RenderSlotsAsync(json, LabResults.RowTemplate, context: new TileContext([], TileOptions.Default, new ValueFormat { Decimals = 2 }));

        Assert.Multiple(() =>
        {
            Assert.That(FxHtml.PlainText(cells["test"]), Is.EqualTo("Potassium"));
            Assert.That(FxHtml.PlainText(cells["value"]), Is.EqualTo("4.51 mmol/L"));
            Assert.That(FxHtml.PlainText(cells["range"]), Is.EqualTo("3.5 - 5.1 mmol/L"));
            Assert.That(cells["flag"], Does.Contain("High").And.Contain("fx-badge-warning"));
        });
    }
}
