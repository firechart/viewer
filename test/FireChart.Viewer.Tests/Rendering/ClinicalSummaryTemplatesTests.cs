using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// The rows of the lists: one tile template per type, the page says what it shows above the list (TileContext) and the row leaves that out
public class ClinicalSummaryTemplatesTests
{
    private const string Patient = """
        { "resourceType": "Patient", "id": "p1", "name": [ { "family": "Smith", "given": [ "Ann" ] } ], "gender": "female", "birthDate": "1986-03-02",
          "identifier": [ { "value": "MRN-12345" } ] }
        """;

    private const string Visit = """
        { "resourceType": "Encounter", "id": "e1", "status": "finished", "identifier": [ { "value": "V-77" } ], "type": [ { "text": "Office visit" } ],
          "period": { "start": "2020-03-01T10:15:00+03:00", "end": "2020-03-01T11:00:00+03:00" }, "subject": { "reference": "Patient/p1" } }
        """;

    private const string Condition = """
        { "resourceType": "Condition", "id": "c1", "code": { "text": "Bronchitis" }, "clinicalStatus": { "coding": [ { "code": "active" } ] },
          "subject": { "reference": "Patient/p1" }, "onsetDateTime": "2020-02-27" }
        """;

    private const string Procedure = """
        { "resourceType": "Procedure", "id": "pr1", "status": "completed", "code": { "text": "Chest X-ray" }, "subject": { "reference": "Patient/p1" },
          "performedDateTime": "2020-03-01T10:40:00+03:00" }
        """;

    private const string Observation = """
        { "resourceType": "Observation", "id": "o1", "status": "final", "code": { "text": "Heart rate" }, "subject": { "reference": "Patient/p1" },
          "encounter": { "reference": "Encounter/e1" }, "valueQuantity": { "value": 150, "unit": "bpm" }, "interpretation": [ { "coding": [ { "code": "H", "display": "High" } ] } ],
          "effectiveDateTime": "2020-03-01T10:20:00+03:00" }
        """;

    private static readonly VendorTemplates s_vendor = new();

    private static readonly TileContext s_summary = new(["Patient/p1"], TileOptions.Default);

    private static readonly TileContext s_visit = new(["Patient/p1", "Encounter/e1"], TileOptions.Default);

    private static Task<TileModel> TileAsync(string json, TileContext? context = null)
    {
        var pool = FhirRenderer.CreatePool([new PoolEntry(Patient), new PoolEntry(Visit)]);
        return new FhirRenderer([s_vendor], null, FixedClock.Offset(3)).RenderTileAsync(json, pool: pool, context: context);
    }

    [Test]
    public async System.Threading.Tasks.Task AnObservation_NamesThePatientAndTheVisit_OnlyWhenThePageDoesNotShowThemAbove()
    {
        var root = await TileAsync(Observation);
        var summary = await TileAsync(Observation, s_summary);
        var visit = await TileAsync(Observation, s_visit);

        Assert.Multiple(() =>
        {
            Assert.That(root.Text, Is.EqualTo("Heart rate 2020-03-01 10:20 · Ann Smith, female, 1986-03-02, MRN-12345 · V-77, 2020-03-01 10:15 - 2020-03-01 11:00, Office visit 150 bpm High final"));
            Assert.That(summary.Text, Is.EqualTo("Heart rate 2020-03-01 10:20 · V-77, 2020-03-01 10:15 - 2020-03-01 11:00, Office visit 150 bpm High final"));
            Assert.That(visit.Text, Is.EqualTo("Heart rate 2020-03-01 10:20 150 bpm High final"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task ARecordOfAnotherPatient_NamesItsPatient_EvenBelowABanner()
    {
        var other = Observation.Replace("Patient/p1", "Patient/p2", StringComparison.Ordinal);

        var tile = await TileAsync(other, s_summary);

        Assert.That(tile.Text, Does.Contain("Patient/p2"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheOptions_ShowTheAgeAtTheEvent_AndHideTheStatus()
    {
        var options = new TileContext(["Patient/p1"], new TileOptions(ShowAge: true, ShowStatus: false));

        var tile = await TileAsync(Observation, options);

        Assert.That(tile.Text, Is.EqualTo("Heart rate 2020-03-01 10:20 (33 y) · V-77, 2020-03-01 10:15 - 2020-03-01 11:00, Office visit 150 bpm High"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheTiles_OfConditionProcedureAndObservation_AreTheNameTheMomentAndTheStateAsBadges()
    {
        var condition = await TileAsync(Condition, s_summary);
        var procedure = await TileAsync(Procedure, s_summary);
        var observation = await TileAsync(Observation, s_visit);

        Assert.Multiple(() =>
        {
            Assert.That(condition.Text, Is.EqualTo("Bronchitis 2020-02-27 active"));
            Assert.That(condition.Html, Does.Contain("fx-badge-success"));
            Assert.That(procedure.Text, Is.EqualTo("Chest X-ray 2020-03-01 10:40 completed"));
            Assert.That(observation.Html, Does.Contain("fx-tile-value").And.Contain("fx-badge-warning"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task AnEncounter_IsItsPatientAndNumber_InAListOfItsOwn_AndItsKind_BelowTheBannerOfThePatient()
    {
        var root = await TileAsync(Visit);
        var summary = await TileAsync(Visit, s_summary);

        Assert.Multiple(() =>
        {
            Assert.That(root.Text, Is.EqualTo("Ann Smith, 1986-03-02 | V-77 Office visit - 2020-03-01 10:15 - 2020-03-01 11:00 finished"));
            Assert.That(summary.Text, Is.EqualTo("Office visit V-77 - 2020-03-01 10:15 - 2020-03-01 11:00 finished"));
        });
    }

    [TestCase("""{ "start": "2022-01-01", "end": "2022-01-02" }""", "2022-01-01 - 2022-01-02")]
    [TestCase("""{ "start": "2022-01-01" }""", "2022-01-01 - …")]
    [TestCase("""{ "end": "2022-01-02" }""", "… - 2022-01-02")]
    [TestCase("""{ }""", "")]
    public async System.Threading.Tasks.Task APeriod_ShowsAnOpenEndAsEllipsis_AndNothingWhenBothEndsAreEmpty(string period, string expected)
    {
        var json = $$"""{ "resourceType": "Procedure", "id": "p", "status": "done", "code": { "text": "X" }, "performedPeriod": {{period}} }""";

        var tile = await new FhirRenderer([s_vendor], null, FixedClock.Offset(3)).RenderTileAsync(json);

        Assert.That(tile.Text, Is.EqualTo(string.Join(' ', new[] { "X", expected, "done" }.Where(part => part.Length > 0))));
    }

    [TestCase("1.30", "1.30 mg")]
    [TestCase("150", "150 mg")]
    [TestCase("0.5", "0.5 mg")]
    public async System.Threading.Tasks.Task AQuantity_KeepsTheTrailingZerosOfItsValue(string value, string expected)
    {
        var json = $$"""{ "resourceType": "Observation", "id": "o", "status": "final", "code": { "text": "X" }, "valueQuantity": { "value": {{value}}, "unit": "mg" } }""";

        var tile = await new FhirRenderer([s_vendor], null, FixedClock.Offset(3)).RenderTileAsync(json);

        Assert.That(tile.Text, Is.EqualTo($"X {expected} final"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheTileOfAnObservation_JoinsTheValuesOfItsComponents()
    {
        const string json = """
            { "resourceType": "Observation", "id": "bp", "status": "final", "code": { "text": "Blood pressure" },
              "component": [ { "code": { "text": "Sys" }, "valueQuantity": { "value": 120, "unit": "mmHg" } }, { "code": { "text": "Dia" }, "valueQuantity": { "value": 80, "unit": "mmHg" } } ] }
            """;

        var tile = await new FhirRenderer([s_vendor], null, FixedClock.Offset(3)).RenderTileAsync(json, context: new TileContext([], new TileOptions(ShowStatus: false)));

        Assert.That(tile.Text, Is.EqualTo("Blood pressure 120 mmHg / 80 mmHg"));
    }

    [Test]
    public async System.Threading.Tasks.Task ThePatientBanner_HasNoLinkToItself_UnlessAskedTo()
    {
        var html = FxHtml.Sanitize(await new FhirRenderer([s_vendor]).RenderAsync(Patient, "banner"), link => link);

        Assert.That(html, Does.Contain("""<h2 class="fx-banner-title">Ann Smith</h2>"""));
    }
}
