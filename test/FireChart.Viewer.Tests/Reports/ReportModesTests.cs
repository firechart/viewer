using Bunit;
using FireChart.Viewer.Core;
using Flox.Shared.Components;

namespace FireChart.Viewer.Tests.Reports;

// Show draws an element in a mode: by the component {Type}.{mode} of the library with the element as %record, else by the built-in view of the mode;
// a tile leads with the icon of its type unless it leads with its own
public class ReportModesTests
{
    private const string Patient = """{ "resourceType": "Patient", "id": "p1", "gender": "female" }""";

    private static ResourceSummary Condition(string id, string text, string status, string subject = "Patient/p1")
    {
        return new ResourceSummary(id, "Condition", $$"""
            { "resourceType": "Condition", "id": "{{id}}", "code": { "text": "{{text}}" }, "onsetDateTime": "2020-03-01",
              "subject": { "reference": "{{subject}}", "display": "Bob" },
              "clinicalStatus": { "coding": [ { "code": "{{status}}" } ] } }
            """);
    }

    private static readonly ResourceSummary Encounter = new("e1", "Encounter", """
        { "resourceType": "Encounter", "id": "e1", "status": "finished", "class": { "code": "AMB" }, "type": [ { "text": "Check-up" } ],
          "period": { "start": "2021-05-04" } }
        """);

    private static readonly Dictionary<string, string> Components = new()
    {
        ["Lists"] = """
            <Component name="Lists">
              <Data>
                <Search id="conditions" query="Condition?patient={{%context.id}}"/>
                <Search id="encounters" query="Encounter?patient={{%context.id}}"/>
              </Data>
              <Show value="{{%context}}" mode="banner"/>
              <Section title="Conditions"><Show value="{{%conditions}}" mode="tile"/></Section>
              <Section title="Encounters"><Show value="{{%encounters}}" mode="tile"/></Section>
              <Section title="Encounter"><Show value="{{%encounters}}" mode="view"/></Section>
            </Component>
            """,
        ["Condition.tile"] = """
            <Component name="Condition.tile">
              <Param name="record" required="true"/>
              <Tile title="{{%record.code}}" subtitle="{{%record.onset}}" patient="{{%record.subject}}">
                <Leading icon="healing"/>
                <Trailing><Status value="{{%record.clinicalStatus}}"/></Trailing>
              </Tile>
            </Component>
            """,
        ["Patient.banner"] = """
            <Component name="Patient.banner">
              <Param name="record" required="true"/>
              <Banner title="{{%record.id}}">
                <Facts><Value value="{{%record.gender}}"/></Facts>
              </Banner>
            </Component>
            """,
    };

    [Test]
    public async Task AnElement_IsDrawnByTheComponentOfItsMode_ElseByTheBuiltInOne()
    {
        var reader = new TestReader(Patient, link => link.StartsWith("https://fhir.test/r4/Condition", StringComparison.Ordinal)
                                                     ? [Condition("c1", "Asthma", "active"), Condition("c2", "Flu", "resolved", "Patient/p2")]
                                                     : [Encounter]);
        var (bundle, _, scope) = await TestReport.LoadAsync(new TextAssets(Components), "Lists", Patient, reader);
        Assert.That(bundle.Problems, Is.Empty);

        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var report = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();
        report.WaitForState(() => report.FindAll("[data-report-loading]").Count == 0 && report.FindAll(".fx-summary-row").Count == 3);

        var sections = report.FindAll("section");
        var conditions = sections[0].QuerySelectorAll(".fx-summary-row");
        var encounter = sections[1].QuerySelector(".fx-summary-row")!;
        Assert.Multiple(() =>
        {
            Assert.That(report.Find(".fx-banner-title").TextContent, Is.EqualTo("p1"), "Patient.banner");
            Assert.That(report.Find(".fx-banner-facts span").TextContent, Is.EqualTo("female"));
            Assert.That(conditions.Select(row => row.QuerySelector(".fx-tile-title")!.TextContent), Is.EqualTo(new[] { "Asthma", "Flu" }), "Condition.tile");
            Assert.That(conditions[0].QuerySelector(".fx-badge-success")?.TextContent, Is.EqualTo("active"));
            Assert.That(conditions.Select(row => row.QuerySelector(".fx-tile-subtitle")!.TextContent), Is.EqualTo(new[] { "3/1/2020", "3/1/2020 · Bob" }),
                        "the patient of a record, unless it is the record of the report");
            Assert.That(conditions[0].QuerySelectorAll(".fx-summary-icon"), Has.Length.EqualTo(1), "its own leading, not the icon of the type as well");
            Assert.That(report.FindComponents<FloxIcon>().Select(icon => icon.Instance.Icon), Has.Some.EqualTo("healing"));
            Assert.That(encounter.QuerySelector(".fx-tile-title")!.TextContent, Is.EqualTo("Check-up"), "the built-in tile: the title of the record");
            Assert.That(encounter.QuerySelector(".fx-tile-subtitle")!.TextContent, Does.Contain("5/4/2021"), "when it happened");
            Assert.That(encounter.QuerySelector(".fx-badge-success")?.TextContent, Is.EqualTo("finished"), "its status");
            Assert.That(sections[2].QuerySelectorAll(".fx-label").Select(label => label.TextContent), Does.Contain("Status").And.Contain("Class").And.Contain("Period"),
                        "the built-in view: the elements of the record");
        });
    }
}
