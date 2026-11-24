using Flox.Shared.Components;
using Bunit;
using FireChart.Viewer.Core;

namespace FireChart.Viewer.Tests.Reports;

// The displays of a Feed: the laboratory (a group per report with its results in a table: the value with the decimals of the section, the range, the
// interpretations) and the trends (a measure per code, the chosen one charted)
public class ReportFeedDisplayTests
{
    private const string Patient = """{ "resourceType": "Patient", "id": "p1" }""";

    private static string Observation(string id, string code, string value, string date, string extra = "")
    {
        return $$"""
            { "resourceType": "Observation", "id": "{{id}}", "status": "final", "subject": { "reference": "Patient/p1" }, "effectiveDateTime": "{{date}}",
              "code": { "coding": [ { "system": "http://loinc.org", "code": "{{code}}" } ], "text": "Test {{code}}" },
              "valueQuantity": { "value": {{value}}, "unit": "mmol/L" }{{extra}} }
            """;
    }

    private static async Task<IRenderedComponent<Microsoft.AspNetCore.Components.IComponent>> RenderAsync(BunitContext context, string component, TestReader reader)
    {
        var (bundle, _, scope) = await TestReport.LoadAsync(new TextAssets(new Dictionary<string, string> { ["S"] = component }), "S", Patient, reader);
        Assert.That(bundle.Problems, Is.Empty);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var report = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();
        return report;
    }

    [Test]
    public async Task TheLaboratory_GroupsTheResultsUnderTheirReport_WithTheDecimalsOfTheSection()
    {
        const string component = """
            <Component name="S">
              <Feed title="Results" display="lab-results" decimals="2">
                <Query type="DiagnosticReport"/>
              </Feed>
            </Component>
            """;
        var sodium = Observation("o1", "2951-2", "141.23456", "2024-03-01T08:00:00Z",
                                 """, "referenceRange": [ { "low": { "value": 135, "unit": "mmol/L" }, "high": { "value": 145, "unit": "mmol/L" } } ], "interpretation": [ { "coding": [ { "code": "H" } ], "text": "High" } ]""");
        var report = new ResourceSummary("r1", "DiagnosticReport", """
            { "resourceType": "DiagnosticReport", "id": "r1", "status": "final", "subject": { "reference": "Patient/p1" }, "code": { "text": "Panel" },
              "effectiveDateTime": "2024-03-01T08:00:00Z", "result": [ { "reference": "Observation/o1" } ] }
            """);
        var reader = new TestReader(Patient, link => link.StartsWith("DiagnosticReport?", StringComparison.Ordinal) ? [report] : [],
                                    related: link => link.StartsWith("DiagnosticReport?", StringComparison.Ordinal) ? [new RelatedResource(sodium, "Observation/o1")] : []);

        await using var context = new BunitContext();
        var page = await RenderAsync(context, component, reader);
        page.WaitForState(() => page.FindAll(".fx-lab-table tbody tr").Count == 1);

        var cells = page.FindAll(".fx-lab-table tbody td").Select(cell => cell.TextContent.Trim()).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(page.Find(".fx-lab-head .fx-tile-title").TextContent, Is.EqualTo("Panel"), "the report heads its group");
            Assert.That(cells, Is.EqualTo(new[] { "Test 2951-2", "141.23 mmol/L", "135 - 145 mmol/L", "High" }));
            Assert.That(page.Find(".fx-lab-flag .fx-badge-warning").TextContent, Is.EqualTo("High"));
        });
    }

    // On a narrow screen the laboratory is tiles: the value of an observation in its tile keeps the decimals of the section too
    [Test]
    public async Task ATileOfASectionWithDecimals_WritesItsValueWithThem()
    {
        const string component = """
            <Component name="S">
              <Feed title="Results" decimals="2">
                <Query type="Observation"/>
              </Feed>
            </Component>
            """;
        var glucose = new ResourceSummary("o1", "Observation", Observation("o1", "2345-7", "64.67494101040693", "2024-03-01T08:00:00Z"));
        var reader = new TestReader(Patient, link => link.StartsWith("Observation?", StringComparison.Ordinal) ? [glucose] : []);
        var assets = new MergedAssets(TestLibrary.Assets, new Dictionary<string, string> { ["S"] = component });

        var (bundle, _, scope) = await TestReport.LoadAsync(assets, "S", Patient, reader, FireChart.Viewer.Library.VendorLibrary.UrlOf);
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var page = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();
        page.WaitForState(() => page.FindAll(".fx-tile-title").Count == 1 && page.FindAll("[data-report-loading]").Count == 0, TimeSpan.FromSeconds(10));

        Assert.That(page.Markup, Does.Contain("64.67 mmol/L").And.Not.Contain("64.674"));
    }

    [Test]
    public async Task TheTrends_ShowAMeasurePerCode_AndChartTheChosenOne()
    {
        const string component = """
            <Component name="S">
              <Feed title="Vitals" display="observation-trends">
                <Query type="Observation" params="category=vital-signs"/>
              </Feed>
            </Component>
            """;
        var reader = new TestReader(Patient, link => link.StartsWith("Observation?", StringComparison.Ordinal) && !link.Contains("code=", StringComparison.Ordinal)
            ? [new("w2", "Observation", Observation("w2", "29463-7", "72", "2024-03-01T08:00:00Z")),
               new("w1", "Observation", Observation("w1", "29463-7", "70", "2024-01-01T08:00:00Z")),
               new("h1", "Observation", Observation("h1", "8302-2", "170", "2024-01-01T08:00:00Z"))]
            : []);

        await using var context = new BunitContext();
        var page = await RenderAsync(context, component, reader);
        page.WaitForState(() => page.FindAll(".fx-trend-choice").Count == 2 && page.FindAll(".fx-trends-title").Count == 1);

        Assert.Multiple(() =>
        {
            Assert.That(page.FindAll(".fx-trend-choice").Select(choice => choice.GetAttribute("aria-pressed")), Is.EqualTo(new[] { "true", "false" }));
            Assert.That(page.Find(".fx-trends-title").TextContent, Is.EqualTo("Test 29463-7"), "the first measure is charted");
        });

        await page.FindAll(".fx-trend-choice")[1].ClickAsync(new());
        page.WaitForState(() => page.Find(".fx-trends-title").TextContent == "Test 8302-2");
    }
}
