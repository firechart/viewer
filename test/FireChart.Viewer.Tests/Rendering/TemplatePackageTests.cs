using System.Text.RegularExpressions;
using FireChart.Viewer.Core;
using FireChart.Viewer.Library;
using Hl7.Fhir.Serialization;
using Resource = Hl7.Fhir.Model.Resource;

namespace FireChart.Viewer.Tests.Rendering;

// The check of the template library (src/FireChart.Viewer.Library/wwwroot), the static files a client reads one by one
public partial class TemplatePackageTests
{
    private static readonly string s_package = FindPackage();

    private static string FindPackage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FireChart.Viewer.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The solution folder is not found"), "src", "FireChart.Viewer.Library");
    }

    private static Dictionary<string, string> Files()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(s_package, VendorLibrary.WebRoot, VendorLibrary.TemplatesFolder), "*.liquid", System.IO.SearchOption.AllDirectories))
        {
            var key = Path.GetFileNameWithoutExtension(path);
            Assert.That(result.ContainsKey(key), Is.False, $"The key {key} is in two files");
            result[key] = File.ReadAllText(path);
        }

        return result;
    }

    [Test]
    public void EveryTemplate_Parses_AndEveryPartialItRendersExists()
    {
        var files = Files();

        Assert.That(files, Is.Not.Empty);
        foreach (var (key, text) in files)
        {
            Assert.That(FhirRenderer.Validate(text), Is.Null, key);
            foreach (Match partial in RenderTag().Matches(text))
            {
                Assert.That(files.ContainsKey(Path.GetFileNameWithoutExtension(partial.Groups[1].Value)), Is.True, $"{key} renders {partial.Groups[1].Value}, which is not in the package");
            }
        }
    }

    [Test]
    public async System.Threading.Tasks.Task EverySample_RendersThroughTheLibrary()
    {
        var samples = Directory.EnumerateFiles(Path.Combine(s_package, VendorLibrary.WebRoot, TemplateSamples.Folder), "*.json").ToList();
        var problems = new List<string>();
        var renderer = new FhirRenderer([new MemoryTemplateSource(Files())], problems.Add);

        Assert.That(samples, Is.Not.Empty);
        foreach (var sample in samples)
        {
            var resource = new FhirJsonDeserializer().Deserialize<Resource>(await File.ReadAllTextAsync(sample));
            var html = await renderer.RenderAsync(resource);
            Assert.That(html, Is.Not.Empty, Path.GetFileName(sample));
        }

        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void EveryTemplate_IsWhereAClientLooksForIt()
    {
        var root = Path.Combine(s_package, VendorLibrary.WebRoot);
        var misplaced = Directory.EnumerateFiles(Path.Combine(root, VendorLibrary.TemplatesFolder), "*.liquid", System.IO.SearchOption.AllDirectories)
                                 .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
                                 .Where(path => VendorLibrary.TemplatePathOf(Path.GetFileNameWithoutExtension(path)) != path)
                                 .ToList();

        Assert.That(misplaced, Is.Empty, "a client finds a template by its key only (VendorLibrary.TemplatePathOf)");
    }

    // The client loads a template when the engine asks for it, with the partials it renders: the rows are the ones of the whole library
    [Test]
    public async System.Threading.Tasks.Task ALibraryLoadedOnDemand_RendersAsTheWholeOne_ReadingOnlyWhatItNeeds()
    {
        var assets = TestLibrary.Assets;
        var json = """{ "resourceType": "StructureDefinition", "id": "x1", "name": "USCorePatient", "kind": "resource", "type": "Patient", "derivation": "constraint", "status": "active" }""";

        var onDemand = await new FhirRenderer([new WebVendorTemplates(assets)]).RenderTileAsync(json);
        var whole = await new FhirRenderer([TestLibrary.Vendor]).RenderTileAsync(json);

        Assert.Multiple(() =>
        {
            Assert.That(onDemand.Html, Is.EqualTo(whole.Html));
            Assert.That(assets.Read, Does.Contain("_content/FireChart.Viewer.Library/templates/resources/StructureDefinition@tile.liquid").And.Contain("_content/FireChart.Viewer.Library/templates/partials/status-badge.liquid"));
            Assert.That(assets.Read.Count(path => path.Contains("/templates/", StringComparison.Ordinal)), Is.LessThan(20), "only what the row needs");
        });
    }

    [Test]
    public async System.Threading.Tasks.Task ATileLoadedOnDemand_NamesItsPatientAndTheAge_AsTheWholeLibraryDoes()
    {
        const string patient = """{ "resourceType": "Patient", "id": "p1", "name": [ { "family": "Smith" } ], "birthDate": "1986-03-02" }""";
        const string observation = """
            { "resourceType": "Observation", "id": "o1", "status": "final", "code": { "text": "Heart rate" }, "subject": { "reference": "Patient/p1" },
              "effectiveDateTime": "2020-03-01" }
            """;
        var context = new TileContext([], new TileOptions(ShowAge: true));

        var onDemand = await new FhirRenderer([new WebVendorTemplates(TestLibrary.Assets)]).RenderTileAsync(observation, pool: FhirRenderer.CreatePool([new PoolEntry(patient)]), context: context);
        var whole = await new FhirRenderer([TestLibrary.Vendor]).RenderTileAsync(observation, pool: FhirRenderer.CreatePool([new PoolEntry(patient)]), context: context);

        Assert.That(onDemand.Text, Is.EqualTo(whole.Text).And.Contain("Smith").And.Contain("(33"), "slot_data renders partials the template does not name");
    }

    [GeneratedRegex(@"\{%-?\s*render\s+""([^""]+)""")]
    private static partial Regex RenderTag();
}
