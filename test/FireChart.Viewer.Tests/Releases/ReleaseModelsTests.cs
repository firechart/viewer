using FireChart.Viewer.Core;
using Hl7.Fhir.Introspection;

namespace FireChart.Viewer.Tests.Releases;

// The model of the release of a server: the SDK of that release, so that a template sees the elements as the release declares them
public class ReleaseModelsTests
{
    private const string Encounter = """{ "resourceType": "Encounter", "id": "e-1", "status": "completed", "actualPeriod": { "start": "2026-01-02T10:00:00Z" }, "class": [ { "coding": [ { "code": "AMB" } ] } ] }""";

    private static ReleaseModels Models()
    {
        return new ReleaseModels(new NoReleaseAssemblyLoader());
    }

    [TestCase(null, "R4")]
    [TestCase("", "R4")]
    [TestCase("r5", "R5")]
    [TestCase(" R4B ", "R4B")]
    [TestCase("stu3", "STU3")]
    [TestCase("R6", "R6")]
    [TestCase("R7", "R4")]
    public void TheNameOfTheRelease_IsNormalized(string? release, string expected)
    {
        Assert.That(ReleaseModels.Normalize(release), Is.EqualTo(expected));
    }

    [Test]
    public async Task EveryRelease_HasItsOwnModel()
    {
        var models = Models();
        var byRelease = new Dictionary<string, ModelInspector>();
        foreach (var release in new[] { "STU3", "R4", "R4B", "R5", "R6" })
        {
            byRelease[release] = await models.GetAsync(release);
        }

        Assert.Multiple(() =>
        {
            Assert.That(byRelease.Values.Distinct().Count(), Is.EqualTo(5));
            Assert.That(byRelease["R5"].FindClassMapping("Encounter")!.FindMappedElementByName("actualPeriod"), Is.Not.Null, "R5 has Encounter.actualPeriod");
            Assert.That(byRelease["R4"].FindClassMapping("Encounter")!.FindMappedElementByName("actualPeriod"), Is.Null, "R4 has not");
            Assert.That(byRelease["STU3"].FindClassMapping("Encounter"), Is.Not.Null);
            Assert.That(byRelease["R6"].FindClassMapping("Encounter"), Is.Not.Null);
        });
    }

    [Test]
    public async Task TheLoader_IsAskedOnceForAReleaseOtherThanR4()
    {
        var loader = new CountingLoader();
        var models = new ReleaseModels(loader);

        await models.GetAsync("R4");
        await models.GetAsync("R5");
        await models.GetAsync("R5");

        Assert.That(loader.Requested, Is.EqualTo(["R5"]));
    }

    [Test]
    public async Task AnR5Resource_IsReadWithTheModelOfR5()
    {
        var r5 = FireChart.Viewer.Core.Reports.ReportExpressions.Parse(Encounter, await Models().GetAsync("R5"));

        Assert.Multiple(() =>
        {
            Assert.That(FireChart.Viewer.Core.Reports.ReportText.Children(r5, "class").Select(node => node.Poco.TypeName), Is.EqualTo(new[] { "CodeableConcept" }),
                        "class is a list of CodeableConcept in R5");
            Assert.That(FireChart.Viewer.Core.Reports.ReportText.Children(r5, "actualPeriod").Any(), Is.True, "an element of R5 only");
        });
    }

    private sealed class CountingLoader : IReleaseAssemblyLoader
    {
        public List<string> Requested { get; } = [];

        public Task EnsureAsync(string release, CancellationToken cancellationToken = default)
        {
            Requested.Add(release);
            return Task.CompletedTask;
        }
    }
}
