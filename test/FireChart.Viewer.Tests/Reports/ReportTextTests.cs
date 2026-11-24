using System.Globalization;
using FireChart.Viewer.Core.Reports;
using Hl7.Fhir.Model;

namespace FireChart.Viewer.Tests.Reports;

// How a date and a code read (architecture.md, "Dates and time on the clinical timeline", "Codes are shown as words"), whatever the zone of the machine
public class ReportTextTests
{
    private static readonly CultureInfo s_english = CultureInfo.GetCultureInfo("en-US");
    private static readonly TimeZoneInfo s_tokyo = TimeZoneInfo.CreateCustomTimeZone("test+9", TimeSpan.FromHours(9), "test+9", "test+9");

    private static PocoNode Parse(string json)
    {
        return ReportExpressions.Parse(json, ModelInfo.ModelInspector);
    }

    private static string TextOf(string element, string json, ReportFormat format)
    {
        return Spaces(ReportText.Text(Parse("""{ "resourceType": "Observation", "status": "final", "code": { "text": "x" }, """ + json + " }"), element, format));
    }

    // The data of the cultures differ by platform: ICU (Linux) writes a narrow no-break space before AM, Windows a space
    private static string Spaces(string text)
    {
        return text.Replace(' ', ' ').Replace(' ', ' ');
    }

    [TestCase("1986-05-05", "5/5/1986", TestName = "A date is never moved by a zone")]
    [TestCase("2020-03", "March 2020", TestName = "A month is written in the culture")]
    [TestCase("2020", "2020", TestName = "A year stays")]
    [TestCase("2020-03-01T23:30:00Z", "3/2/2020 8:30 AM", TestName = "A moment with an offset is shown in the zone of the display")]
    [TestCase("2020-03-01T10:00:00", "3/1/2020 10:00 AM (no time zone)", TestName = "A moment without an offset is not converted and says so")]
    public void ADate_ReadsByItsKind(string value, string expected)
    {
        var format = new ReportFormat(s_english, s_tokyo);

        Assert.That(Spaces(ReportText.Date(value, format)), Is.EqualTo(expected));
    }

    [Test]
    public void APeriodOfOneDay_EndsWithItsTimeAlone_InTheZoneOfTheDisplay()
    {
        var format = new ReportFormat(s_english, s_tokyo);

        Assert.Multiple(() =>
        {
            Assert.That(TextOf("effective", """ "effectivePeriod": { "start": "2020-03-01T00:00:00Z", "end": "2020-03-01T01:30:00Z" } """, format),
                        Is.EqualTo("3/1/2020 9:00 AM – 10:30 AM"));
            Assert.That(TextOf("effective", """ "effectivePeriod": { "start": "2020-03-01T00:00:00Z", "end": "2020-03-01T20:00:00Z" } """, format),
                        Is.EqualTo("3/1/2020 9:00 AM – 3/2/2020 5:00 AM"), "the next day in the zone of the display");
        });
    }

    [Test]
    public void ACode_IsItsDisplay_ElseTheWordOfTheTerminologyServer_ElseTheTextOfTheConcept_ElseTheCode()
    {
        var words = new Words { [("http://loinc.org", "8867-4")] = "Heart rate" };
        var format = new ReportFormat(s_english, s_tokyo, words);

        string Concept(string concept)
        {
            return TextOf("value", "\"valueCodeableConcept\": " + concept, format);
        }

        Assert.Multiple(() =>
        {
            Assert.That(Concept("""{ "coding": [ { "system": "http://loinc.org", "code": "8867-4", "display": "Pulse" } ], "text": "HR" }"""), Is.EqualTo("Pulse"));
            Assert.That(Concept("""{ "coding": [ { "system": "http://loinc.org", "code": "8867-4" } ], "text": "HR" }"""), Is.EqualTo("Heart rate"));
            Assert.That(Concept("""{ "coding": [ { "system": "http://loinc.org", "code": "1-1" } ], "text": "HR" }"""), Is.EqualTo("HR"));
            Assert.That(Concept("""{ "coding": [ { "system": "http://loinc.org", "code": "1-1" } ] }"""), Is.EqualTo("1-1"));
            Assert.That(words.Asked, Is.EquivalentTo(new[] { ("http://loinc.org", "8867-4"), ("http://loinc.org", "1-1") }), "only a code without a display is looked up");
        });
    }

    [TestCase("1974-12-25", "51 Years")]
    [TestCase("1974-11-17", "52 Years", TestName = "The birthday counts")]
    [TestCase("1974", "52 Years", TestName = "A year of birth is its first day")]
    [TestCase("2026-05-20", "5 Months")]
    [TestCase("2026-11-10", "7 Days")]
    [TestCase("2026-11-18", null, TestName = "A birth after the day has no age")]
    [TestCase("1974-02-30", null, TestName = "A date that does not exist has no age")]
    public void AnAge_IsWholeYears_ElseMonths_ElseDays(string birth, string? expected)
    {
        var age = ReportText.AgeOf(birth, new DateOnly(2026, 11, 17));

        Assert.That(age is null ? null : $"{age.Count} {age.Unit}", Is.EqualTo(expected));
    }

    [Test]
    public void TheDayOfAnEvent_IsInTheZoneOfTheDisplay_AndThatOfAPeriodItsStart()
    {
        var format = new ReportFormat(s_english, s_tokyo);
        var observation = Parse("""
            { "resourceType": "Observation", "status": "final", "code": { "text": "x" },
              "effectivePeriod": { "start": "2020-03-01T20:00:00Z" }, "issued": "2020-03-01T10:00:00Z" }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(ReportText.DayOfEvent(ReportText.Children(observation, "effective").Single(), format), Is.EqualTo(new DateOnly(2020, 3, 2)));
            Assert.That(ReportText.DayOfEvent(ReportText.Children(observation, "issued").Single(), format), Is.EqualTo(new DateOnly(2020, 3, 1)));
            Assert.That(ReportText.DayOfEvent(ReportText.Children(observation, "status").Single(), format), Is.Null);
        });
    }

    // The words a terminology server would give
    private sealed class Words : Dictionary<(string, string), string>, IReportDisplays
    {
        public HashSet<(string, string)> Asked { get; } = [];

        public string? DisplayOf(string system, string code)
        {
            Asked.Add((system, code));
            return TryGetValue((system, code), out var word) ? word : null;
        }
    }
}
