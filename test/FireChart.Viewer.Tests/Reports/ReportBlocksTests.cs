using Bunit;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Reports;

namespace FireChart.Viewer.Tests.Reports;

// The blocks of a list and of a record: a table of the results, statuses and interpretations with their tones, a line, a link, a notice, details,
// values joined; and where a Column and the children of a Table may stand
public class ReportBlocksTests
{
    private const string Patient = """
        { "resourceType": "Patient", "id": "p1", "gender": "female", "birthDate": "1974-12-25",
          "generalPractitioner": [ { "reference": "Practitioner/d1", "display": "Dr. Gray" } ] }
        """;

    private static ResourceSummary Lab(string id, string test, string value, string interpretation, string status)
    {
        return new ResourceSummary(id, "Observation", $$"""
            { "resourceType": "Observation", "id": "{{id}}", "status": "{{status}}", "code": { "text": "{{test}}" },
              "valueQuantity": { "value": {{value}}, "unit": "mmol/L" },
              "interpretation": [ { "coding": [ { "code": "{{interpretation}}", "display": "{{interpretation}}" } ] } ] }
            """);
    }

    private const string Component = """
        <Component name="Labs">
          <Data><Search id="labs" query="Observation?patient={{%context.id}}"/></Data>
          <Notice tone="danger" title="Note"><Value value="{{%context.id}}"/></Notice>
          <Section title="Labs" count="{{%labs.count()}}">
            <Table each="{{%labs}}" as="o">
              <Column title="Test" value="{{%o.code}}"/>
              <Column title="Result" value="{{%o.value}}"><Interpretation value="{{%o.interpretation.first()}}"/></Column>
              <Column title="Status"><Status value="{{%o.status}}"/></Column>
            </Table>
          </Section>
          <Details title="More">
            <Row main="{{%context.gender}}" meta="{{%context.birthDate}}"><Link value="{{%context.generalPractitioner}}"/></Row>
          </Details>
          <Value value="{{%labs.code}}" separator=" | "/>
          <Row><Main><Link value="{{%context.generalPractitioner}}"/></Main>meta</Row>
          <Text><Parts separator=" - "><Value value="{{%context.gender}}"/><Value value="{{%context.deceased}}"/>{{%context.birthDate}}</Parts></Text>
        </Component>
        """;

    [Test]
    public async Task TheBlocks_ShowAListAndARecord_WithTheTonesOfTheirCodes()
    {
        var reader = new TestReader(Patient, _ => [Lab("o1", "Sodium", "150", "H", "final"), Lab("o2", "Potassium", "4.1", "N", "cancelled")]);
        var (bundle, _, scope) = await TestReport.LoadAsync(new TextAssets(new Dictionary<string, string> { ["Labs"] = Component }), "Labs", Patient, reader);
        Assert.That(bundle.Problems, Is.Empty);

        await using var context = new BunitContext();
        var report = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();
        report.WaitForState(() => report.FindAll("[data-report-loading]").Count == 0);

        var rows = report.FindAll("tbody tr");
        Assert.Multiple(() =>
        {
            Assert.That(report.FindAll("th").Select(th => th.TextContent), Is.EqualTo(new[] { "Test", "Result", "Status" }));
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].TextContent, Does.Contain("Sodium").And.Contain("150 mmol/L"));
            Assert.That(rows[0].QuerySelector(".fx-badge-warning")?.TextContent, Is.EqualTo("H"), "an interpretation H is a warning");
            Assert.That(rows[0].QuerySelector(".fx-badge-success")?.TextContent, Is.EqualTo("final"), "a status final is a success");
            Assert.That(rows[1].QuerySelector(".fx-badge-error")?.TextContent, Is.EqualTo("cancelled"));
            Assert.That(report.Find(".fx-count").TextContent, Is.EqualTo("2"));
            Assert.That(report.Find(".fx-notice-danger").TextContent, Does.Contain("Note").And.Contain("p1"));
            Assert.That(report.Find("details summary").TextContent, Is.EqualTo("More"));
            Assert.That(report.Find(".fx-row-main").TextContent, Is.EqualTo("female"));
            Assert.That(report.Find(".fx-row").TextContent, Does.Contain("Dr. Gray"), "a link without a Viewer around is its text");
            Assert.That(report.Markup, Does.Contain("Sodium | Potassium"));
            Assert.That(report.FindAll(".fx-row-main")[^1].TextContent, Is.EqualTo("Dr. Gray"), "a Main part: a link in the main of a row");
            Assert.That(report.Nodes.Select(node => node.TextContent), Has.Some.EqualTo("female - 12/25/1974"), "Parts: the empty part drops its separator");
        });
    }

    [Test]
    public void AColumn_StandsOnlyInATable_AndATableHoldsOnlyColumns()
    {
        var component = ReportXml.Parse("""
            <Component name="C">
              <Column title="Lost"/>
              <Table each="{{%context.identifier}}"><Section/></Table>
            </Component>
            """);

        var problems = ReportCheck.Check(component, _ => null).Select(problem => problem.Message).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(problems, Has.Some.Contains("<Column> stands only in <Table>"));
            Assert.That(problems, Has.Some.Contains("<Section> is not allowed in <Table>; only Column"));
        });
    }
}
