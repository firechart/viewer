using Bunit;
using FireChart.Viewer.Core.Reports;

namespace FireChart.Viewer.Tests.Reports;

// A page in a right-to-left language (Hebrew): a value keeps the order of its own text (bdi), a number, a date or a quantity reads left to right;
// a space between a text and a block on the same line is text, a line break is the layout of the XML
public class ReportBidiTests
{
    private const string Patient = """
        { "resourceType": "Patient", "id": "p1", "birthDate": "1986-05-05", "name": [ { "text": "Suzi Bechtelar" } ],
          "identifier": [ { "value": "7c8e5aa3" } ] }
        """;

    private const string Component = """
        <Component name="B">
          <Text>ID <Value value="{{%context.identifier.first()}}"/></Text>
          <Value value="{{%context.birthDate}}"/>
          <Value value="{{%context.name.first()}}"/>
        </Component>
        """;

    [Test]
    public async Task AValue_IsIsolated_ADateReadsLeftToRight_AndTheSpaceBeforeABlockIsKept()
    {
        var (bundle, _, scope) = await TestReport.LoadAsync(new TextAssets(new Dictionary<string, string> { ["B"] = Component }), "B", Patient,
                                                           new TestReader(Patient, _ => []));
        Assert.That(bundle.Problems, Is.Empty);

        await using var context = new BunitContext();
        var report = context.Render(scope.Render(bundle.Root!.Body));
        report.WaitForState(() => report.FindAll("bdi").Count == 3);

        var values = report.FindAll("bdi");
        Assert.Multiple(() =>
        {
            Assert.That(report.Markup, Does.Contain("ID <bdi"), "the space between the text and the value");
            Assert.That(values[0].GetAttribute("dir"), Is.EqualTo("ltr"), "an identifier");
            Assert.That(values[1].GetAttribute("dir"), Is.EqualTo("ltr"), "a date");
            Assert.That(values[2].GetAttribute("dir"), Is.Null, "a name");
            Assert.That(ReportXml.Parse("<Component name=\"L\">\n  <Text>a</Text>\n  <Text>b</Text>\n</Component>").Body.Select(node => node.Name),
                        Is.EqualTo(new[] { "Text", "Text" }), "a line break between blocks is not text");
        });
    }
}
