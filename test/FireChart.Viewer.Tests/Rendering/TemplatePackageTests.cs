using System.Text.RegularExpressions;
using FireChart.Viewer.Core;
using FireChart.Viewer.Library;
using Hl7.Fhir.Serialization;
using Resource = Hl7.Fhir.Model.Resource;

namespace FireChart.Viewer.Tests.Rendering;

// The check of the template library (src/FireChart.Viewer.Library), the one FireChart.Viewer.Library embeds
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
        foreach (var path in Directory.EnumerateFiles(Path.Combine(s_package, "templates"), "*.liquid", System.IO.SearchOption.AllDirectories))
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
        var samples = Directory.EnumerateFiles(Path.Combine(s_package, "samples"), "*.json").ToList();
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
    public void TheLibraryInTheAssembly_IsTheLibraryOfThePackage()
    {
        var vendor = new VendorTemplates();
        var files = Files();

        Assert.That(vendor.Keys.Order(StringComparer.Ordinal), Is.EqualTo(files.Keys.Order(StringComparer.Ordinal)));
        foreach (var (key, text) in files)
        {
            Assert.That(TemplateHash.Of(vendor.Find(key)!), Is.EqualTo(TemplateHash.Of(text)), key);
        }

        Assert.That(TemplateSamples.Names, Is.EquivalentTo(Directory.EnumerateFiles(Path.Combine(s_package, "samples"), "*.json").Select(Path.GetFileNameWithoutExtension)));
    }

    [GeneratedRegex(@"\{%-?\s*render\s+""([^""]+)""")]
    private static partial Regex RenderTag();
}
