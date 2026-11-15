using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Viewer;

// The run time reads a search form laxly (an older format upgraded, what is not understood left out); the authoring is strict (the schema, ViewerJson.Read)
[TestFixture]
public class SearchFormReaderTests
{
    [Test]
    public void AFormOfFormat1_IsUpgraded()
    {
        var result = SearchFormReader.Read("""
                                           { "formatVersion": 1, "name": "old", "authoredForVersion": "4.3.0",
                                             "fields": [ { "queryName": "gender", "included": false }, { "queryName": "name", "included": true } ] }
                                           """);

        Assert.Multiple(() =>
        {
            Assert.That(result.Problems, Is.Empty);
            Assert.That(result.Form!.FormatVersion, Is.EqualTo(SearchFormDefinition.CurrentFormatVersion));
            Assert.That(result.Form.FhirRelease, Is.EqualTo("R4B"));
            Assert.That(result.Form.Fields.Select(f => (f.QueryName, f.Hidden)), Is.EqualTo(new[] { ("gender", true), ("name", false) }));
        });
    }

    [Test]
    public void WhatIsNotUnderstood_IsLeftOut_AndTheRestOfTheFormStays()
    {
        var result = SearchFormReader.Read("""
                                           { "name": "lax", "formatVersion": 2, "colour": "red",
                                             "groups": [ { "code": "who", "order": "first" }, { "code": "when" } ],
                                             "fields": [
                                               { "queryName": "gender", "control": "slider", "tooltip": "x" },
                                               { "label": { "en-US": "No query name" } },
                                               "not an object",
                                               { "queryName": "birthdate", "control": "dateRange", "groupCode": "when" }
                                             ] }
                                           """);

        Assert.Multiple(() =>
        {
            Assert.That(result.Form!.Name, Is.EqualTo("lax"));
            Assert.That(result.Form.Groups.Select(g => g.Code), Is.EqualTo(new[] { "when" }));
            Assert.That(result.Form.Fields.Select(f => f.QueryName), Is.EqualTo(new[] { "gender", "birthdate" }));
            Assert.That(result.Form.Fields[0].Control, Is.EqualTo(new SearchFormField().Control), "an unknown control gives way to the default");
            Assert.That(result.Form.Fields[1].Control, Is.EqualTo(SearchFieldControl.DateRange));
            Assert.That(result.Problems.Select(p => p.Path), Is.EquivalentTo(new[] { "colour", "fields/0/control", "fields/0/tooltip", "groups/0", "fields/1", "fields/2" }));
            Assert.That(result.Problems, Has.All.Property(nameof(FormProblem.Level)).EqualTo(FormProblemLevel.Warning));
        });
    }

    [Test]
    public void AFormOfANewerFormat_IsReadAsFarAsItIsUnderstood()
    {
        var result = SearchFormReader.Read("""{ "formatVersion": 99, "name": "future", "fields": [ { "queryName": "code", "valueSetBinding": {} } ] }""");

        Assert.Multiple(() =>
        {
            Assert.That(result.Form!.Fields.Single().QueryName, Is.EqualTo("code"));
            Assert.That(result.Problems.Select(p => p.Path), Is.EqualTo(new[] { "formatVersion", "fields/0/valueSetBinding" }));
        });
    }

    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("null")]
    [TestCase("""{ "title": { "en-US": "No name" } }""")]
    public void OnlyAFileThatGivesNoFormAtAll_IsAnError(string text)
    {
        var result = SearchFormReader.Read(text);

        Assert.Multiple(() =>
        {
            Assert.That(result.Form, Is.Null);
            Assert.That(result.Problems.Single().Level, Is.EqualTo(FormProblemLevel.Error));
        });
    }

    // The strict side: the schema the editors check the files with is the model's, and every vendor form is read without a problem
    [Test]
    public void TheSchemaOfTheLibrary_IsTheOneOfTheModel()
    {
        var file = Path.Combine(PublicRoot(), "library", "schemas", "search-form.schema.json");
        var schema = ViewerJson.SchemaOf<SearchFormDefinition>(SchemaId, "FireChart search form");
        if (Environment.GetEnvironmentVariable("FIRECHART_WRITE_GENERATED") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, schema);
        }

        Assert.That(File.Exists(file) ? File.ReadAllText(file).ReplaceLineEndings("\n") : null, Is.EqualTo(schema),
                    "the model changed: run the tests with FIRECHART_WRITE_GENERATED=1 and commit the schema");
        Assert.That(schema, Does.Contain("\"additionalProperties\": false").And.Contain("\"dateRange\""));
    }

    [Test]
    public void EveryVendorForm_IsReadWithoutAProblem()
    {
        var problems = Directory.EnumerateFiles(Path.Combine(PublicRoot(), "library", VendorSearchForms.Folder), "*.json", System.IO.SearchOption.AllDirectories)
                                .SelectMany(file => SearchFormReader.Read(File.ReadAllText(file)).Problems.Select(p => $"{file}: {p.Path} {p.Message}"));

        Assert.That(problems, Is.Empty);
    }

    public const string SchemaId = "https://firechart.org/schemas/search-form.schema.json";

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
