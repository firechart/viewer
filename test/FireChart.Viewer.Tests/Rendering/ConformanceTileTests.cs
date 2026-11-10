using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// The vendor tiles of the conformance resources (the lists of the Conformance Browser over the FHIR facade): built from the summary elements only,
// the name first as on the pages of the browser, the facts joined by " · ", the status at the end
public class ConformanceTileTests
{
    private static readonly VendorTemplates s_vendor = TestLibrary.Vendor;

    public sealed record Sample(string Type, string Json, string Title, string Subtitle, string[] Ending)
    {
        public override string ToString()
        {
            return Type;
        }
    }

    private static Sample S(string type, string body, string title, string subtitle, params string[] ending)
    {
        return new Sample(type, $$"""{ "resourceType": "{{type}}", "id": "x1", "status": "active", {{body}} }""", title, subtitle, ending);
    }

    private static readonly Sample[] s_samples =
    [
        S("StructureDefinition", """ "name": "USCorePatient", "title": "US Core Patient", "kind": "resource", "type": "Patient", "derivation": "constraint", "abstract": true """,
          "USCorePatient", "resource · Patient · constraint", "abstract", "active"),
        S("ValueSet", """ "title": "Administrative Gender", "url": "http://hl7.org/fhir/ValueSet/administrative-gender", "version": "4.0.1" """,
          "Administrative Gender", "http://hl7.org/fhir/ValueSet/administrative-gender · 4.0.1", "active"),
        S("CodeSystem", """ "name": "AdministrativeGender", "url": "http://hl7.org/fhir/administrative-gender", "content": "complete", "count": 4 """,
          "AdministrativeGender", "http://hl7.org/fhir/administrative-gender", "4 concepts", "complete", "active"),
        S("SearchParameter", """ "name": "gender", "code": "gender", "base": [ "Patient", "Person" ], "type": "token" """,
          "gender", "gender · Patient, Person", "token", "active"),
        S("OperationDefinition", """ "name": "Everything", "code": "everything", "resource": [ "Patient" ], "kind": "operation" """,
          "Everything", "$everything · Patient", "operation", "active"),
        S("CapabilityStatement", """ "name": "Base", "kind": "capability", "fhirVersion": "4.0.1", "software": { "name": "FireChart" } """,
          "Base", "FireChart · 4.0.1", "capability", "active"),
        S("ConceptMap", """ "name": "SexToGender", "url": "http://example.org/cm", "version": "1" """,
          "SexToGender", "http://example.org/cm · 1", "active"),
        S("NamingSystem", """ "name": "SSN", "kind": "identifier", "uniqueId": [ { "type": "uri", "value": "http://hl7.org/fhir/sid/us-ssn" } ] """,
          "SSN", "http://hl7.org/fhir/sid/us-ssn", "identifier", "active"),
        S("ImplementationGuide", """ "name": "USCore", "packageId": "hl7.fhir.us.core", "version": "6.1.0", "fhirVersion": [ "4.0.1" ] """,
          "USCore", "hl7.fhir.us.core · 6.1.0 · 4.0.1", "active"),
        S("StructureMap", """ "title": "Map", "url": "http://example.org/sm", "version": "2" """,
          "Map", "http://example.org/sm · 2", "active")
    ];

    [TestCaseSource(nameof(s_samples))]
    public async System.Threading.Tasks.Task ATile_IsTheName_TheFactsOfItsType_AndItsBadges(Sample sample)
    {
        var problems = new List<string>();

        var tile = await new FhirRenderer([s_vendor], problems.Add, FixedClock.Offset(0)).RenderTileAsync(sample.Json);
        var html = System.Net.WebUtility.HtmlDecode(tile.Html);

        Assert.Multiple(() =>
        {
            Assert.That(problems, Is.Empty);
            Assert.That(html, Does.Contain($"<div class=\"fx-tile-title\">{sample.Title}</div>"), sample.Type);
            Assert.That(html, Does.Contain($"<div class=\"fx-tile-subtitle\">{sample.Subtitle}</div>"), sample.Type);
            foreach (var badge in sample.Ending)
            {
                Assert.That(html, Does.Match($"fx-tile-end\">.*{System.Text.RegularExpressions.Regex.Escape(badge)}"), sample.Type);
            }
        });
    }

    [TestCaseSource(nameof(s_samples))]
    public async System.Threading.Tasks.Task ABareResource_IsARowByItsId_AndItsTextIsItsId(Sample sample)
    {
        var problems = new List<string>();
        var renderer = new FhirRenderer([s_vendor], problems.Add, FixedClock.Offset(0));
        var json = $$"""{ "resourceType": "{{sample.Type}}", "id": "bare-1" }""";

        var tile = await renderer.RenderTileAsync(json);
        var text = await renderer.RenderAsync(json, "text");
        var named = await renderer.RenderAsync(sample.Json, "text");

        Assert.Multiple(() =>
        {
            Assert.That(problems, Is.Empty);
            Assert.That(tile.Html, Does.Contain("<div class=\"fx-tile-title\">bare-1</div>").And.Not.Contain("fx-tile-subtitle").And.Not.Contain("fx-tile-end"));
            Assert.That(text.Trim(), Is.EqualTo("bare-1"));
            Assert.That(System.Net.WebUtility.HtmlDecode(named).Trim(), Is.EqualTo(sample.Title), sample.Type);
        });
    }
}
