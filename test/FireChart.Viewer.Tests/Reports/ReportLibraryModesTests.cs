using Bunit;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Reports;

// Every component of a mode of the vendor library ({Type}.{mode}) draws a record of its type without a failure: the smallest record (its type and
// id) and, for the patient, the samples of the library. A failure of an expression or a block is a note in the page, which this test finds
public class ReportLibraryModesTests
{
    private static IEnumerable<TestCaseData> Modes()
    {
        var folder = Path.Combine(TestLibrary.WebRoot(), Core.Reports.ReportLibrary.Folder);
        return Directory.GetFiles(folder, "*.xml")
                        .Select(Path.GetFileNameWithoutExtension)
                        .Where(name => name!.Split('.') is [_, "tile" or "view" or "banner"])
                        .Order(StringComparer.Ordinal)
                        .Select(name => new TestCaseData(name).SetName($"Mode_{name}"));
    }

    [TestCaseSource(nameof(Modes))]
    public async Task AComponentOfAMode_DrawsTheSmallestRecordOfItsType(string name)
    {
        var parts = name.Split('.');
        var record = $$"""{ "resourceType": "{{parts[0]}}", "id": "x1" }""";
        var host = """<Component name="Host"><Show value="{{%context}}" mode="MODE"/></Component>""".Replace("MODE", parts[1], StringComparison.Ordinal);
        var assets = new MergedAssets(TestLibrary.Assets, new Dictionary<string, string> { ["Host"] = host });
        var reader = new TestReader(record, _ => []);
        var (bundle, _, scope) = await TestReport.LoadAsync(assets, "Host", record, reader, VendorLibrary.UrlOf);
        Assert.That(bundle.Problems, Is.Empty);

        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var report = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();
        report.WaitForState(() => report.FindAll("[data-report-loading]").Count == 0);

        Assert.That(report.FindAll("[data-report-note]").Select(note => note.TextContent), Is.Empty);
    }

    [TestCase("Patient-full.json")]
    [TestCase("Patient-minimal.json")]
    public async Task ThePatientComponents_DrawTheSamplesOfTheLibrary(string sample)
    {
        var record = await File.ReadAllTextAsync(Path.Combine(TestLibrary.WebRoot(), "samples", sample));
        const string host = """
            <Component name="Host">
              <Show value="{{%context}}" mode="banner"/>
              <Show value="{{%context}}" mode="tile"/>
              <Show value="{{%context}}" mode="view"/>
            </Component>
            """;
        var assets = new MergedAssets(TestLibrary.Assets, new Dictionary<string, string> { ["Host"] = host });
        var reader = new TestReader(record, _ => []);
        var (bundle, _, scope) = await TestReport.LoadAsync(assets, "Host", record, reader, VendorLibrary.UrlOf);

        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var report = context.Render(scope.Render(bundle.Root!.Body));
        reader.Gate.SetResult();
        report.WaitForState(() => report.FindAll("[data-report-loading]").Count == 0);

        Assert.Multiple(() =>
        {
            Assert.That(report.FindAll("[data-report-note]").Select(note => note.TextContent), Is.Empty);
            Assert.That(report.FindAll(".fx-banner-title"), Has.Count.EqualTo(1));
        });
    }

}
