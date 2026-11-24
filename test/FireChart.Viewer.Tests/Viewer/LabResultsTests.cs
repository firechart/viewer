using Bunit;
using FireChart.Viewer.Components;
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

    // A row without the name of its test shows its id, which comes from the server unchecked: as text, never as markup
    [Test]
    public void ARowWithoutItsTestCell_ShowsTheIdOfTheServerAsText()
    {
        const string id = "<img src=x onerror=alert(1)>";
        var section = new SummarySection(new SummaryFormSection { Code = "labs" }, [], "en");
        using var context = new BunitContext();

        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var view = context.Render<FireChart.Viewer.Reports.ReportLab>(parameters => parameters
            .Add(component => component.Section, section)
            .Add(component => component.Groups, [new LabGroup("day:2020-03-01", null, new DateOnly(2020, 3, 1), [Obs(id, "2020-03-01T08:00:00Z")])])
            .Add(component => component.Tile, (ResourceSummary item) => (Microsoft.AspNetCore.Components.RenderFragment)(builder => builder.AddContent(0, item.Id)))
            .Add(component => component.Text, (ResourceSummary _, string _) => string.Empty)
            .Add(component => component.Flag, (ResourceSummary _) => (Microsoft.AspNetCore.Components.RenderFragment)(_ => { }))
            .Add(component => component.HasFlag, (ResourceSummary _) => false)
            .Add(component => component.HrefOf, (ResourceSummary _) => null));

        var cell = view.Find("td.fx-lab-test");
        Assert.Multiple(() =>
        {
            Assert.That(cell.QuerySelector("img"), Is.Null);
            Assert.That(cell.TextContent.Trim(), Is.EqualTo(id));
        });
    }

    // A narrow section draws the tiles of the results and no table, a wide one the table and no tile of a result: only the form the width shows
    [Test]
    public async Task OnlyTheFormTheWidthShows_IsDrawn()
    {
        var section = new SummarySection(new SummaryFormSection { Code = "labs" }, [], "en");
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var view = context.Render<FireChart.Viewer.Reports.ReportLab>(parameters => parameters
            .Add(component => component.Section, section)
            .Add(component => component.Groups, [new LabGroup("day:2020-03-01", null, new DateOnly(2020, 3, 1), [Obs("o1", "2020-03-01T08:00:00Z")])])
            .Add(component => component.Tile, (ResourceSummary item) => (Microsoft.AspNetCore.Components.RenderFragment)(builder => builder.AddContent(0, "tile " + item.Id)))
            .Add(component => component.Text, (ResourceSummary _, string _) => string.Empty)
            .Add(component => component.Flag, (ResourceSummary _) => (Microsoft.AspNetCore.Components.RenderFragment)(_ => { }))
            .Add(component => component.HasFlag, (ResourceSummary _) => false)
            .Add(component => component.HrefOf, (ResourceSummary _) => null));
        var wide = (view.FindAll("table.fx-lab-table").Count, view.FindAll(".fx-lab-narrow").Count);

        await view.InvokeAsync(() => view.Instance.OnWidth(true));

        Assert.Multiple(() =>
        {
            Assert.That(wide, Is.EqualTo((1, 0)), "the table until the width is known");
            Assert.That(view.FindAll("table.fx-lab-table"), Is.Empty);
            Assert.That(view.Find(".fx-lab-narrow.fx-lab-chosen").TextContent.Trim(), Is.EqualTo("tile o1"));
        });
    }
}
