using FireChart.Viewer.Core.Reports;

namespace FireChart.Viewer.Tests.Reports;

// A search brings what its tiles name (their patient, their encounter), so a tile reads nothing more
public class ReportNeedsTests
{
    [Test]
    public void TheRecordsATileNames_AreIncludedByTheReferenceParameterOfTheirElement()
    {
        var encounter = ReportXml.Parse(File.ReadAllText(Path.Combine(TestLibrary.WebRoot(), "components", "Encounter.tile.xml")));
        var observation = ReportXml.Parse("""
            <Component name="Observation.tile">
              <Param name="record" required="true"/>
              <Tile title="{{%record.code}}" patient="{{%record.subject}}" encounter="{{%record.encounter}}"/>
            </Component>
            """);
        var computed = ReportXml.Parse("""
            <Component name="Condition.tile">
              <Param name="record" required="true"/>
              <Tile title="{{%record.code}}" patient="{{%record.subject.where(true)}}"/>
            </Component>
            """);

        Assert.Multiple(() =>
        {
            Assert.That(ReportNeeds.OfTile(encounter, "Encounter")!.Include, Is.EqualTo(new[] { "Encounter:subject" }));
            Assert.That(ReportNeeds.OfTile(observation, "Observation")!.Include, Is.EqualTo(new[] { "Observation:subject", "Observation:encounter" }));
            Assert.That(ReportNeeds.OfTile(computed, "Condition"), Is.Null, "an expression that is not an element of the record");
        });
    }
}
