using FireChart.Tools.FhirMetadata;

namespace FireChart.Viewer.Tests.Viewer;

// The metadata in FireChart.Viewer.Core is what the generator gives for the core packages now: run public/tools/FireChart.FhirMetadata when this fails.
// It needs the core packages in the cache of the tool (the tool downloads them); without them it is inconclusive
[TestFixture]
public class FhirMetadataGeneratorTests
{
    [TestCaseSource(typeof(FhirMetadataGenerator), nameof(FhirMetadataGenerator.Releases))]
    public void The_generated_metadata_is_current(CoreRelease release)
    {
        var folder = CorePackages.Folder(release.PackageName, release.PackageVersion);
        Assume.That(Directory.Exists(folder), $"{release.PackageName}@{release.PackageVersion} is not in the cache of the tool");

        var generated = FhirMetadataGenerator.Generate(folder, release);
        var committed = File.ReadAllText(Path.Combine(PublicRoot(), "src", "FireChart.Viewer.Core", "Fhir", "Metadata", release.ClassName + ".g.cs"));

        Assert.That(generated, Is.EqualTo(committed.ReplaceLineEndings("\n")), $"run {FhirMetadataGenerator.Tool}");
    }

    [Test]
    public void The_hint_of_a_shared_parameter_is_the_line_of_its_type()
    {
        const string description = "Multiple Resources: \n\n* [Patient](patient.html): A patient identifier\n* [Person](person.html): A person Identifier";

        Assert.Multiple(() =>
        {
            Assert.That(FhirMetadataGenerator.ExtractHint(description, "Person"), Is.EqualTo("A person Identifier"));
            Assert.That(FhirMetadataGenerator.ExtractHint(description, "Group"), Is.Null);
            Assert.That(FhirMetadataGenerator.ExtractHint("The [date](datatypes.html) of birth, e.g. 1970. More text", "Patient"), Is.EqualTo("The date of birth, e.g. 1970"));
        });
    }

    private static string PublicRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FireChart.Viewer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory!.FullName;
    }
}
