using FireChart.Viewer.Pages;

namespace FireChart.Viewer.Tests.Viewer;

// The component observation-trends: the measures of a section by code, and the lines of the chart of one of them
public class ObservationTrendsTests
{
    private static ResourceSummary Weight(string id, string when, double kg, string unit = "kg")
    {
        return new ResourceSummary(id, "Observation", $$"""
            { "resourceType": "Observation", "id": "{{id}}", "status": "final", "effectiveDateTime": "{{when}}",
              "code": { "coding": [ { "system": "http://loinc.org", "code": "29463-7", "display": "Body Weight" } ] },
              "valueQuantity": { "value": {{kg.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "unit": "{{unit}}" } }
            """);
    }

    private static ResourceSummary Pressure(string id, string when, int systolic, int diastolic)
    {
        return new ResourceSummary(id, "Observation", $$"""
            { "resourceType": "Observation", "id": "{{id}}", "status": "final", "effectiveDateTime": "{{when}}",
              "code": { "coding": [ { "system": "http://loinc.org", "code": "85354-9", "display": "Blood pressure" } ] },
              "component": [
                { "code": { "coding": [ { "system": "http://loinc.org", "code": "8480-6", "display": "Systolic" } ] }, "valueQuantity": { "value": {{systolic}}, "unit": "mm[Hg]" } },
                { "code": { "coding": [ { "system": "http://loinc.org", "code": "8462-4", "display": "Diastolic" } ] }, "valueQuantity": { "value": {{diastolic}}, "unit": "mm[Hg]" } } ] }
            """);
    }

    [Test]
    public void TheMeasures_AreTheCodesOfTheRecords_InTheOrderTheyCame_WithTheirSearchToken()
    {
        var groups = ObservationTrends.Group([Pressure("p2", "2020-02-01T10:00:00Z", 130, 85), Weight("w2", "2020-01-20T10:00:00Z", 70), Pressure("p1", "2020-01-01T10:00:00Z", 120, 80)]);

        Assert.Multiple(() =>
        {
            Assert.That(groups.Select(group => $"{group.Search}: {string.Join(",", group.Records.Select(record => record.Id))}"),
                        Is.EqualTo(new[] { "http://loinc.org|85354-9: p2,p1", "http://loinc.org|29463-7: w2" }));
        });
    }

    [Test]
    public void BloodPressure_IsTwoLines_OldestFirst()
    {
        var lines = ObservationTrends.SeriesOf([Pressure("p2", "2020-02-01T10:00:00Z", 130, 85), Pressure("p1", "2020-01-01T10:00:00Z", 120, 80)], TimeZoneInfo.Utc);

        Assert.That(lines.Select(line => $"{line.Name} {line.Unit}: {string.Join(",", line.Points.Select(point => point.Value))}"),
                    Is.EqualTo(new[] { "Systolic mm[Hg]: 120,130", "Diastolic mm[Hg]: 80,85" }));
    }

    [Test]
    public void APointOfAnotherUnit_IsLeftOut_TheUnitOfMostPointsWins()
    {
        var lines = ObservationTrends.SeriesOf([Weight("a", "2020-01-01T10:00:00Z", 70), Weight("b", "2020-01-02T10:00:00Z", 154, "lb"), Weight("c", "2020-01-03T10:00:00Z", 71)],
                                               TimeZoneInfo.Utc);

        Assert.That(lines.Select(line => $"{line.Name} {line.Unit}: {string.Join(",", line.Points.Select(point => point.Value))}"), Is.EqualTo(new[] { "Body Weight kg: 70,71" }));
    }
}
