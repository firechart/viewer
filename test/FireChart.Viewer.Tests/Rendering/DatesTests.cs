using System.Globalization;
using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// A date is shown as the kind of value it is: a date stays, a time with an offset is shown in the zone of the display, a time without one is not guessed
public class DatesTests
{
    private static readonly TimeZoneInfo s_berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [TestCase("1980-03-01")]
    [TestCase("1980-03")]
    [TestCase("1980")]
    public void ADateWithoutATime_IsNeverMoved(string value)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FhirFilters.Date(value, TimeZoneInfo.CreateCustomTimeZone("far", TimeSpan.FromHours(14), "far", "far")), Is.EqualTo(value));
            Assert.That(FhirFilters.Date(value, TimeZoneInfo.CreateCustomTimeZone("far", TimeSpan.FromHours(-12), "far", "far")), Is.EqualTo(value));
        });
    }

    [TestCase("2020-03-01T10:15:30+03:00", "2020-03-01 08:15")]
    [TestCase("2020-03-01T10:15:30.123+03:00", "2020-03-01 08:15")]
    [TestCase("2020-03-01T10:15:30-05:00", "2020-03-01 16:15")]
    [TestCase("2020-03-01T10:15:30Z", "2020-03-01 11:15")]
    [TestCase("2020-03-01T10:15Z", "2020-03-01 11:15")]
    public void ATimeWithAnOffset_IsShownInTheZoneOfTheDisplay(string value, string expected)
    {
        Assert.That(FhirFilters.Date(value, s_berlin), Is.EqualTo(expected));
    }

    [Test]
    public void TheSameOffsetMeansADifferentTime_InSummerAndInWinter()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FhirFilters.Date("2020-01-15T10:00:00Z", s_berlin), Is.EqualTo("2020-01-15 11:00"));
            Assert.That(FhirFilters.Date("2020-07-15T10:00:00Z", s_berlin), Is.EqualTo("2020-07-15 12:00"));
        });
    }

    [Test]
    public void TheDay_CanChange_WhenTheZoneChanges()
    {
        Assert.That(FhirFilters.Date("2020-03-01T23:30:00-05:00", s_berlin), Is.EqualTo("2020-03-02 05:30"));
    }

    [Test]
    public void ATimeWithoutAnOffset_IsNotConverted_AndSaysSo()
    {
        Assert.That(FhirFilters.Date("2020-03-01T10:15:30", s_berlin), Is.EqualTo("2020-03-01 10:15 (no time zone)"));
    }

    [Test]
    public void WithoutAZone_ATimeIsShownAsItWasRecorded()
    {
        Assert.That(FhirFilters.Date("2020-03-01T10:15:30+03:00"), Is.EqualTo("2020-03-01 10:15"));
    }

    [TestCase("ru-RU", "01.03.2020", "01.03.2020 10:15")]
    [TestCase("de-DE", "01.03.2020", "01.03.2020 10:15")]
    [TestCase("en-US", "3/1/2020", "3/1/2020 10:15 AM")]
    public void ADateAndATime_AreWrittenInTheCultureOfTheApplication(string name, string date, string time)
    {
        var culture = CultureInfo.GetCultureInfo(name);

        Assert.Multiple(() =>
        {
            Assert.That(FhirFilters.Date("2020-03-01", s_berlin, culture), Is.EqualTo(date));
            // newer ICU (CLDR 42 and later, as on the build server) writes a narrow no-break space before AM and PM
            var actual = FhirFilters.Date("2020-03-01T10:15:30+03:00", TimeZoneInfo.CreateCustomTimeZone("z", TimeSpan.FromHours(3), "z", "z"), culture).Replace('\u202f', ' ');
            Assert.That(actual, Is.EqualTo(time));
        });
    }

    [Test]
    public void TheCurrentCulture_IsTheOneTheFilterUsesWhenNoneIsGiven()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

            Assert.That(FhirFilters.Date("2022-01-01"), Is.EqualTo("01.01.2022"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [TestCase("ru-RU", "1,30")]
    [TestCase("de-DE", "1,30")]
    [TestCase("en-US", "1.30")]
    public void ADecimal_KeepsItsDigits_AndUsesTheDecimalSeparatorOfTheCulture(string name, string expected)
    {
        Assert.That(FhirFilters.Number("1.30", CultureInfo.GetCultureInfo(name)), Is.EqualTo(expected));
    }

    [TestCase("150", "150")]
    [TestCase("-0.5", "-0.5")]
    [TestCase("1e3", "1e3")]
    [TestCase("abc", "abc")]
    public void ANumber_WithoutAFraction_OrNotANumber_IsNotInvented(string value, string expected)
    {
        Assert.That(FhirFilters.Number(value, CultureInfo.InvariantCulture), Is.EqualTo(expected));
    }

    [TestCase("ru", "true", "Да")]
    [TestCase("ru", "false", "Нет")]
    [TestCase("de-DE", "true", "Ja")]
    [TestCase("en", "true", "Yes")]
    [TestCase("ja", "false", "No")]
    public void ABoolean_IsAWordOfTheLanguageOfTheApplication(string language, string value, string expected)
    {
        Assert.That(FhirFilters.Bool(value, [new VendorTemplates()], CultureInfo.GetCultureInfo(language)), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("yesterday")]
    [TestCase("2020-03-01T10")]
    [TestCase("2020-03-01T10:15:30+0300")]
    public void WhatIsNotADate_StaysAsItIs(string value)
    {
        Assert.That(FhirFilters.Date(value, s_berlin), Is.EqualTo(value));
    }

    private const string Patient = """{ "resourceType": "Patient", "id": "p1", "birthDate": "1986-03-02" }""";

    private static async System.Threading.Tasks.Task<string> RowAsync(string resource, TimeProvider clock)
    {
        var pool = FhirRenderer.CreatePool([new PoolEntry(Patient, "Patient/p1")]);
        var tile = await new FhirRenderer([new VendorTemplates()], null, clock).RenderTileAsync(resource, pool: pool, context: new TileContext(["Patient/p1"], new TileOptions(ShowAge: true)));
        return tile.Html;
    }

    [Test]
    public async System.Threading.Tasks.Task ARow_ShowsTheAgeAtTheEvent_NotTheAgeToday()
    {
        const string observation = """
            { "resourceType": "Observation", "id": "o1", "status": "final", "code": { "text": "Heart rate" }, "subject": { "reference": "Patient/p1" },
              "effectiveDateTime": "2020-03-01T10:20:00+03:00" }
            """;

        var html = await RowAsync(observation, FixedClock.Offset(1));

        Assert.That(html, Does.Contain("2020-03-01 08:20").And.Contain("(33 y)"));
    }

    [Test]
    public async System.Threading.Tasks.Task ARow_WithoutAPatientInThePool_ShowsNoAge()
    {
        const string observation = """
            { "resourceType": "Observation", "id": "o1", "status": "final", "code": { "text": "Heart rate" }, "subject": { "reference": "Patient/other" },
              "effectiveDateTime": "2020-03-01T10:20:00+03:00" }
            """;

        var html = await RowAsync(observation, FixedClock.Offset(1));

        Assert.That(html, Does.Not.Contain(" y)"));
    }

    [Test]
    public async System.Threading.Tasks.Task ARowBeforeTheBirth_ShowsNoAge()
    {
        const string observation = """
            { "resourceType": "Observation", "id": "o1", "status": "final", "code": { "text": "Heart rate" }, "subject": { "reference": "Patient/p1" },
              "effectiveDateTime": "1985-01-01" }
            """;

        var html = await RowAsync(observation, FixedClock.Offset(1));

        Assert.That(html, Does.Not.Contain(" y)"));
    }

    [TestCase("""{ "resourceType": "Patient", "id": "p1", "birthDate": "1986-03-02", "deceasedDateTime": "2010-03-01T10:00:00Z" }""", "(23 y)")]
    [TestCase("""{ "resourceType": "Patient", "id": "p1", "birthDate": "1986-03-02" }""", "(40 y)")]
    public async System.Threading.Tasks.Task ThePatientBanner_ShowsTheAgeAtDeath_NotTheAgeToday(string patient, string expected)
    {
        var html = await new FhirRenderer([new VendorTemplates()], null, FixedClock.Offset(1)).RenderAsync(patient, "banner");

        Assert.That(FxHtml.Sanitize(html), Does.Contain(expected));
    }
}
