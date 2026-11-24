using System.Globalization;
using Bunit;
using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Reports;
using FireChart.Viewer.Library;
using FireChart.Viewer.Reports;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace FireChart.Viewer.Tests.Reports;

// The components of the vendor library over a FHIR server: every one passes the check, and the report of a patient renders its blocks, each with
// its own search, a component of the project with its parameter and a slot filled by the caller
public class ReportRenderTests
{
    private const string Patient = """
        { "resourceType": "Patient", "id": "p1", "active": false, "gender": "female", "birthDate": "1974-12-25",
          "name": [ { "given": ["Anna"], "family": "Schmidt" } ],
          "identifier": [ { "system": "urn:mrn", "value": "MRN-7" } ] }
        """;

    private const string Weight = """
        { "resourceType": "Observation", "id": "w1", "status": "final", "code": { "text": "Body weight" },
          "valueQuantity": { "value": 72.5, "unit": "kg" }, "effectiveDateTime": "2026-11-01",
          "interpretation": [ { "text": "High" } ] }
        """;

    [Test]
    public async Task EveryComponentOfTheVendorLibrary_PassesTheCheck()
    {
        var library = new ReportLibrary(TestLibrary.Assets, VendorLibrary.UrlOf);
        var names = Directory.GetFiles(Path.Combine(TestLibrary.WebRoot(), ReportLibrary.Folder), "*.xml").Select(Path.GetFileNameWithoutExtension);

        foreach (var name in names)
        {
            var bundle = await library.LoadAsync(name!);
            Assert.That(bundle.Problems, Is.Empty, name);
        }
    }

    [Test]
    public async Task TheReportOfAPatient_ShowsEveryBlockWithItsOwnSearch_AndAskesEachQueryOnce()
    {
        var reader = new TestReader(Patient, link => link.Contains("vital-signs", StringComparison.Ordinal) ? [new ResourceSummary("w1", "Observation", Weight)] : []);
        var (bundle, data, scope) = await TestReport.LoadAsync(TestLibrary.Assets, "PatientReport", Patient, reader, VendorLibrary.UrlOf);

        await using var context = new BunitContext();
        var report = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();

        report.WaitForState(() => report.FindAll("[data-report-loading]").Count == 0);
        var text = report.Markup;
        Assert.Multiple(() =>
        {
            Assert.That(report.Find(".fx-banner-title").TextContent, Is.EqualTo("Anna Schmidt"), "the banner of the patient: Patient.banner");
            Assert.That(text, Does.Contain("12/25/1974"));
            Assert.That(report.Find(".fx-banner-flags").TextContent, Does.Contain("Inactive"), "If over %record.active = false");
            Assert.That(text, Does.Contain("MRN-7"), "Repeat over the identifiers");
            Assert.That(report.FindAll("section .fx-section-title").Select(title => title.FirstChild?.TextContent),
                        Is.EqualTo(new[] { "Vital signs", "Laboratory", "Conditions", "Active medications" }), "ListPanel: a component with a slot");
            Assert.That(report.Find("section .fx-count").TextContent, Is.EqualTo("1"), "the count of the vital signs");
            Assert.That(text, Does.Contain("Body weight").And.Contain("72.5 kg").And.Contain("High"), "ObservationRow: a component with a parameter");
            Assert.That(text, Does.Contain("No laboratory results"));
            Assert.That(reader.Queries, Has.Count.EqualTo(5).And.Unique);
            Assert.That(data.Progress.Messages.Select(loaded => $"{loaded.Type} {loaded.Count}"),
                        Is.EquivalentTo(new[] { "Observation 1", "Observation 0", "Condition 0", "MedicationRequest 0", "Encounter 0" }), "what a load at once tells");
            Assert.That(data.Progress.IsLoading, Is.False);
            Assert.That(data.Progress.Started, Is.Positive);
        });
    }
}
