using Flox.Shared.Components;
using Bunit;
using FireChart.Viewer.Core;

namespace FireChart.Viewer.Tests.Reports;

// A Feed is a section of a summary: the records of its queries about the record of the page, each drawn by the tile of its type, a record that does
// not refer to the page left out (a server that ignores the parameter), the count with "+" while there is a next page, which More loads
public class ReportFeedTests
{
    private const string Patient = """{ "resourceType": "Patient", "id": "p1" }""";

    private static ResourceSummary Condition(string id, string text, string patient = "p1")
    {
        return new ResourceSummary(id, "Condition", $$"""
            { "resourceType": "Condition", "id": "{{id}}", "code": { "text": "{{text}}" }, "subject": { "reference": "Patient/{{patient}}" },
              "recordedDate": "2020-0{{id[^1]}}-01" }
            """);
    }

    private const string Component = """
        <Component name="S">
          <Feed code="problems" title="Problem list">
            <Query type="Condition" params="clinical-status=active" sort="-recorded-date"/>
          </Feed>
        </Component>
        """;

    [Test]
    public async Task AFeed_ShowsTheRecordsOfThePage_ByTheirTiles_AndMoreLoadsTheNextPage()
    {
        var reader = new TestReader(Patient, link => link.EndsWith("&next", StringComparison.Ordinal)
            ? [Condition("c3", "Asthma")]
            : link.StartsWith("Condition?", StringComparison.Ordinal) ? [Condition("c1", "Gout"), Condition("c2", "Flu"), Condition("x1", "Other's", "p2")] : []);
        var (bundle, _, scope) = await TestReport.LoadAsync(new TextAssets(new Dictionary<string, string> { ["S"] = Component }), "S", Patient, reader);
        Assert.That(bundle.Problems, Is.Empty);

        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var report = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();
        report.WaitForState(() => report.FindAll(".fx-tile-title").Count == 2);

        var query = reader.Queries.Single(link => link.StartsWith("Condition?", StringComparison.Ordinal));
        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Contain("patient=p1").And.Contain("clinical-status=active"));
            Assert.That(report.Find(".fx-summary-group-title").TextContent, Is.EqualTo("Problem list"));
            Assert.That(report.FindAll(".fx-tile-title").Select(title => title.TextContent), Is.EqualTo(new[] { "Flu", "Gout" }),
                        "newest first; the record of another patient is left out");
            Assert.That(report.Find(".fx-summary-section .fx-badge").TextContent, Is.EqualTo("2+"));
        });

        await report.FindAll("button").Single(button => button.TextContent.Contains("More", StringComparison.Ordinal)).ClickAsync(new());
        report.WaitForState(() => report.FindAll(".fx-tile-title").Count == 3);
        Assert.That(report.Find(".fx-summary-section .fx-badge").TextContent, Is.EqualTo("3"));
    }

    // The options of a Feed reach the tiles of its records: the age of the patient at the record, no status, the code of a system before the title
    [Test]
    public async Task TheOptionsOfAFeed_GiveItsTilesTheAgeAtTheRecord_TheCodeOfASystem_AndNoStatus()
    {
        const string patient = """{ "resourceType": "Patient", "id": "p1", "birthDate": "1980-01-01" }""";
        const string tile = """
            <Component name="Condition.tile">
              <Param name="record" required="true"/>
              <Tile title="{{%record.code}}" patient="{{%record.subject}}"><Trailing><Status value="{{%record.clinicalStatus}}"/></Trailing></Tile>
            </Component>
            """;
        const string summary = """
            <Component name="S">
              <Feed code="problems" title="Problems" age="true" status="false" codes="http://hl7.org/fhir/sid/icd-10 http://snomed.info/sct">
                <Query type="Condition"/>
              </Feed>
              <Feed code="plain" title="Plain">
                <Query type="Condition"/>
              </Feed>
            </Component>
            """;
        var condition = new ResourceSummary("c1", "Condition", """
            { "resourceType": "Condition", "id": "c1", "subject": { "reference": "Patient/p1" }, "recordedDate": "2020-03-01",
              "clinicalStatus": { "coding": [ { "code": "active" } ] },
              "code": { "coding": [ { "system": "http://snomed.info/sct", "code": "195967001", "display": "Asthma" } ] } }
            """);
        var reader = new TestReader(patient, link => link.StartsWith("Condition?", StringComparison.Ordinal) ? [condition] : []);
        var (bundle, _, scope) = await TestReport.LoadAsync(new TextAssets(new Dictionary<string, string> { ["S"] = summary, ["Condition.tile"] = tile }), "S", patient, reader);
        Assert.That(bundle.Problems, Is.Empty);

        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddFloxComponents();
        var report = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();
        report.WaitForState(() => report.FindAll(".fx-tile-title").Count == 2);

        var sections = report.FindAll(".fx-summary-section");
        Assert.Multiple(() =>
        {
            Assert.That(sections[0].QuerySelector(".fx-tile-code")?.TextContent, Is.EqualTo("195967001"), "the first system the record has");
            Assert.That(sections[0].QuerySelector(".fx-tile-subtitle")?.TextContent, Is.EqualTo("40 y"), "the age at the record, of the patient of the page");
            Assert.That(sections[0].QuerySelector(".fx-tile-end .fx-badge"), Is.Null, "no status");
            Assert.That(sections[1].QuerySelector(".fx-tile-code"), Is.Null);
            Assert.That(sections[1].QuerySelector(".fx-tile-subtitle"), Is.Null);
            Assert.That(sections[1].QuerySelector(".fx-tile-end .fx-badge")?.TextContent, Is.EqualTo("active"));
        });
    }
}
