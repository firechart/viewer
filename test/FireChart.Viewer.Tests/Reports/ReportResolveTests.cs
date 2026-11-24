using Bunit;
using FireChart.Viewer.Core;

namespace FireChart.Viewer.Tests.Reports;

// A reference without a display reads as the title of the record it points to, read once per report; Show draws that record in its mode; Resolve
// gives it to its content; a field whose content shows nothing is not shown at all
public class ReportResolveTests
{
    private const string Patient = """
        { "resourceType": "Patient", "id": "p1",
          "generalPractitioner": [ { "reference": "Practitioner/d1" } ],
          "managingOrganization": { "reference": "Organization/gone" } }
        """;

    private const string Doctor = """{ "resourceType": "Practitioner", "id": "d1", "name": [ { "family": "Gray", "given": ["Ann"], "prefix": ["Dr."] } ] }""";

    private const string Component = """
        <Component name="R">
          <Field label="Doctor"><Link value="{{%context.generalPractitioner}}"/></Field>
          <Field label="Again"><Link value="{{%context.generalPractitioner}}"/></Field>
          <Field label="Organization"><Link value="{{%context.managingOrganization}}"/></Field>
          <Field label="Nobody"><Link value="{{%context.link.other}}"/></Field>
          <Section title="Doctors"><Show value="{{%context.generalPractitioner}}" mode="tile"/></Section>
          <Section title="Organizations"><Show value="{{%context.managingOrganization}}" mode="tile"/></Section>
          <Section title="Tile"><Tile title="Visit" patient="{{%context.managingOrganization}}"/></Section>
          <Resolve value="{{%context.generalPractitioner}}" as="doctor">
            <Field label="Family" value="{{%doctor.name.family}}"/>
          </Resolve>
          <Resolve value="{{%context.managingOrganization}}" as="organization">
            <Field label="Lost" value="{{%organization.name}}"/>
            <Otherwise><Field label="Fallback" value="{{%context.managingOrganization}}"/></Otherwise>
          </Resolve>
        </Component>
        """;

    [Test]
    public async Task AReference_ReadsAsItsRecord_ReadOnce_AndAnEmptyFieldIsNotShown()
    {
        var reader = new TestReader(Patient, _ => [], (type, id) => type == "Practitioner" && id == "d1" ? Doctor : null);
        var (bundle, _, scope) = await TestReport.LoadAsync(new TextAssets(new Dictionary<string, string> { ["R"] = Component }), "R", Patient, reader);
        Assert.That(bundle.Problems, Is.Empty);

        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var report = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();
        report.WaitForState(() => report.FindAll("[data-report-loading]").Count == 0);

        var fields = report.FindAll(".fx-field").ToDictionary(field => field.QuerySelector(".fx-label")!.TextContent, field => field.QuerySelector(".fx-value")!.TextContent);
        Assert.Multiple(() =>
        {
            Assert.That(fields["Doctor"], Is.EqualTo("Dr. Ann Gray"), "the title of the record");
            Assert.That(fields["Again"], Is.EqualTo("Dr. Ann Gray"));
            Assert.That(fields["Organization"], Is.EqualTo("Organization could not be found"), "a record the server says does not exist");
            Assert.That(report.FindAll(".fx-field .fx-badge-warning").Select(badge => badge.TextContent), Is.EqualTo(new[] { "Organization could not be found" }),
                        "a badge, not a link to a page that does not exist");
            Assert.That(fields, Does.Not.ContainKey("Nobody"), "no label without a value");
            Assert.That(fields["Family"], Is.EqualTo("Gray"), "Resolve gives the record to its content");
            Assert.That(fields, Does.Not.ContainKey("Lost"), "a record that cannot be read: not the content");
            Assert.That(fields["Fallback"], Is.EqualTo("Organization/gone"), "but its part Otherwise");
            Assert.That(report.Find(".fx-tile-title").TextContent, Is.EqualTo("Dr. Ann Gray"), "Show draws the record in its mode");
            Assert.That(report.FindAll("section").Single(section => section.TextContent.StartsWith("Organizations", StringComparison.Ordinal))
                              .QuerySelector(".fx-badge-warning")?.TextContent, Is.EqualTo("Organization could not be found"), "Show: the badge in its place");
            Assert.That(report.FindAll(".fx-tile-subtitle").Single().TextContent, Is.EqualTo("Organization could not be found"), "the second line of a tile");
            Assert.That(reader.Reads.Count(read => read == "Practitioner/d1"), Is.EqualTo(1), "read once per report");
        });
    }
}
