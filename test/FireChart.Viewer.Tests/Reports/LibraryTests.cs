using FireChart.Viewer.Core;

namespace FireChart.Viewer.Tests.Reports;

// The vendor library is a project of the designer: its manifest, and only the folders the Viewer reads (components, forms, styles, samples, schemas)
public class LibraryTests
{
    // The designer opens the library as a project, and the build of an application writes the version of the release into its manifest
    [Test]
    public void TheLibrary_IsAProjectOfTheDesigner_WithOnlyTheFoldersOfTheViewer()
    {
        var root = TestLibrary.WebRoot();
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "firechart.project.json")));
        var folders = Directory.EnumerateDirectories(root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(manifest.RootElement.GetProperty("name").GetString(), Is.EqualTo("firechart-vendor"));
            Assert.That(manifest.RootElement.GetProperty("version").GetString(), Is.EqualTo("0.0.0-dev"), "the placeholder the build replaces");
            Assert.That(folders, Is.EqualTo(new[] { "components", "locales", "samples", "styles" }),
                        "the AssetFolders and VendorFolders of the designer (project.ts)");
        });
    }

    // Every language of the library translates every text, with the same placeholders
    [TestCase("de")]
    [TestCase("he")]
    public void ALanguageOfTheLibrary_TranslatesEveryText(string culture)
    {
        var root = TestLibrary.WebRoot();
        var english = FireChart.Viewer.Core.Reports.ViewerComponentCheck.ReadTexts(File.ReadAllText(Path.Combine(root, "locales", "strings.resx")));
        var translated = FireChart.Viewer.Core.Reports.ViewerComponentCheck.ReadTexts(File.ReadAllText(Path.Combine(root, "locales", $"strings.{culture}.resx")));

        Assert.Multiple(() =>
        {
            Assert.That(translated.Keys, Is.EquivalentTo(english.Keys));
            Assert.That(english.Where(text => translated.TryGetValue(text.Key, out var other) && other.Count(c => c == '{') != text.Value.Count(c => c == '{'))
                               .Select(text => text.Key), Is.Empty, "the placeholders");
        });
    }

    [Test]
    public void TheHashOfAText_IgnoresLineEndings_ButNotTheText()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TemplateHash.Of("a\r\nb"), Is.EqualTo(TemplateHash.Of("a\nb")));
            Assert.That(TemplateHash.Of("a"), Is.Not.EqualTo(TemplateHash.Of("b")));
        });
    }
}
