using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// The page of a resource must not look complete when it is not: what the templates did not show is added by the engine, and a template cannot switch it off
public class SafetyNetTests
{
    private const string Patient = """
        { "resourceType": "Patient", "id": "p1", "gender": "male", "birthDate": "1974-12-25",
          "name": [ { "family": "Chalmers", "given": [ "Peter" ] } ],
          "telecom": [ { "system": "phone", "value": "555-1234" } ] }
        """;

    private static FhirRenderer Renderer(Dictionary<string, string> templates, List<string>? problems = null)
    {
        return new FhirRenderer([new MemoryTemplateSource(templates)], problems is null ? null : problems.Add);
    }

    private static Dictionary<string, string> Basic()
    {
        return new Dictionary<string, string>
        {
            ["Patient@view"] = "{% apply self.name %}|{{ self.gender }}",
            ["HumanName"] = "{{ self.family }}",
            ["_complex"] = "<span>unrendered</span>",
            ["_primitive"] = "{{ self }}"
        };
    }

    private static Task<string> ViewAsync(string json, Dictionary<string, string> templates, ResourcePool? pool = null)
    {
        return Renderer(templates).RenderViewAsync(json, "view", pool: pool);
    }

    [Test]
    public async System.Threading.Tasks.Task WhenEverythingIsShown_NothingIsAdded()
    {
        var html = await ViewAsync("""{ "resourceType": "Patient", "id": "p1", "meta": { "versionId": "3" }, "text": { "status": "generated", "div": "<div>x</div>" }, "gender": "male", "name": [ { "family": "Chalmers" } ] }""", Basic());

        Assert.That(html, Is.EqualTo("Chalmers|male"));
    }

    [Test]
    public async System.Threading.Tasks.Task AnElementTheTemplatesDidNotRead_IsCountedAndListedUnderOtherData()
    {
        var html = await ViewAsync(Patient, Basic());

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("""<div class="fx-notice fx-notice-warning">Not shown above: 2 elements (birthDate, telecom)."""));
            Assert.That(html, Does.Contain("""<details class="fx-other"><summary>Other data (2)</summary>"""));
            Assert.That(html, Does.Contain("1974-12-25").And.Contain("555-1234").And.Contain("phone"));
            Assert.That(html.IndexOf("Chalmers|male", StringComparison.Ordinal), Is.GreaterThan(html.IndexOf("Not shown above", StringComparison.Ordinal)), "the notice is above the form");
            Assert.That(html.IndexOf("<details", StringComparison.Ordinal), Is.GreaterThan(html.IndexOf("Chalmers|male", StringComparison.Ordinal)), "the data is under it");
        });
    }

    [Test]
    public async System.Threading.Tasks.Task AnElementThatWentToTheGenericTemplate_IsNotCountedAsShown()
    {
        var templates = Basic();
        templates["Patient@view"] = "{% apply self.photo %}{{ self.gender }}";

        var html = await ViewAsync("""{ "resourceType": "Patient", "gender": "male", "photo": [ { "contentType": "image/png", "title": "face" } ] }""", templates);

        Assert.That(html, Does.Contain("Not shown above: 1 element (photo)").And.Contain("face").And.Contain("image/png"));
    }

    [Test]
    public async System.Threading.Tasks.Task ASimpleValueTheGenericTemplatePrints_IsShown()
    {
        var html = await ViewAsync("""{ "resourceType": "Patient", "gender": "male" }""", new Dictionary<string, string> { ["Patient@view"] = "{% apply self.gender %}", ["_primitive"] = "{{ self }}" });

        Assert.That(html, Is.EqualTo("male"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheElementsOfModifierExtension_AreAlwaysShown_AtAnyDepth_EvenWhenTheTemplateReadThem()
    {
        var templates = Basic();
        templates["Patient@view"] = "{{ self.gender }}{% assign m = self.modifierExtension %}{% for c in self.contact %}{{ c.gender }}{% endfor %}";

        var html = await ViewAsync("""
            { "resourceType": "Patient", "gender": "male",
              "modifierExtension": [ { "url": "http://example.org/StructureDefinition/do-not-use", "valueBoolean": true } ],
              "contact": [ { "gender": "female", "modifierExtension": [ { "url": "http://example.org/StructureDefinition/deceased-contact", "valueString": "yes" } ] } ] }
            """, templates);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.StartWith("""<div class="fx-notice fx-notice-danger">"""));
            Assert.That(html, Does.Contain("Modifier extensions: they can change the meaning of this record"));
            Assert.That(html, Does.Contain("""title="http://example.org/StructureDefinition/do-not-use">do-not-use</div>""").And.Contain("true"));
            Assert.That(html, Does.Contain("deceased-contact").And.Contain("yes"));
            Assert.That(html, Does.Not.Contain("Other data"), "they are not counted as not shown");
        });
    }

    [Test]
    public async System.Threading.Tasks.Task AnExtensionInsideAShownElement_IsReported_WhenNobodyRead_It()
    {
        var templates = Basic();
        templates["Patient@view"] = "{% for c in self.contact %}{{ c.gender }}{% endfor %}";

        var html = await ViewAsync("""
            { "resourceType": "Patient",
              "contact": [ { "gender": "female", "extension": [ { "url": "http://example.org/StructureDefinition/preferred", "valueCodeableConcept": { "coding": [ { "display": "Mobile first" } ] } } ] } ] }
            """, templates);

        Assert.That(html, Does.Contain("Not shown above: 1 element (contact.extension)").And.Contain("preferred").And.Contain("Mobile first"));
    }

    [Test]
    public async System.Threading.Tasks.Task AnExtensionThatWasRead_IsShown()
    {
        var templates = Basic();
        templates["Patient@view"] = "{% for e in self.extension %}{{ e.valueString }}{% endfor %}";

        var html = await ViewAsync("""{ "resourceType": "Patient", "extension": [ { "url": "http://example.org/x", "valueString": "seen" } ] }""", templates);

        Assert.That(html, Is.EqualTo("seen"));
    }

    [Test]
    public async System.Threading.Tasks.Task AChoiceElement_ReadByItsPlainName_IsShown()
    {
        var templates = new Dictionary<string, string> { ["Observation@view"] = "{{ self.value.value }}", ["_primitive"] = "{{ self }}" };

        var html = await ViewAsync("""{ "resourceType": "Observation", "valueQuantity": { "value": 150, "unit": "mmHg" } }""", templates);

        Assert.That(html, Is.EqualTo("150"));
    }

    [Test]
    public async System.Threading.Tasks.Task AnotherResourceOfTheSameType_ReadThroughAReference_DoesNotHideWhatTheMainOneHas()
    {
        const string member = """{ "resourceType": "Observation", "id": "m1", "code": { "text": "Member code" } }""";
        var templates = new Dictionary<string, string>
        {
            ["Observation@view"] = "{% assign m = self.hasMember | first | resolve %}{{ m.code.text }}",
            ["_complex"] = "",
            ["_primitive"] = "{{ self }}"
        };

        var html = await ViewAsync("""{ "resourceType": "Observation", "id": "o1", "code": { "text": "Main code" }, "hasMember": [ { "reference": "Observation/m1" } ] }""", templates,
                                   FhirRenderer.CreatePool([new PoolEntry(member)]));

        Assert.That(html, Does.Contain("Member code").And.Contain("Not shown above: 1 element (code)").And.Contain("Main code"));
    }

    [Test]
    public async System.Threading.Tasks.Task WhatServersWrite_CannotInjectMarkup()
    {
        const string hostile = """{ "resourceType": "Patient", "gender": "male", "extension": [ { "url": "http://example.org/\"onmouseover=\"alert(1)", "valueString": "<script>alert(1)</script>" } ] }""";

        var html = await ViewAsync(hostile, new Dictionary<string, string> { ["Patient@view"] = "{{ self.gender }}" });
        var clean = FxHtml.Sanitize(html);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("<script"));
            Assert.That(html, Does.Not.Contain("\"onmouseover"));
            Assert.That(clean, Does.Contain("fx-notice").And.Contain("<details class=\"fx-other\">").And.Not.Contain("<script"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task ARecordTheVendorTemplatesShowInFull_GetsNoNotice()
    {
        const string patient = """
            { "resourceType": "Patient", "id": "p", "active": true, "birthDate": "1974-12-25", "gender": "male",
              "name": [ { "family": "Chalmers", "given": [ "Peter" ] } ],
              "identifier": [ { "value": "MRN-7" } ],
              "telecom": [ { "system": "phone", "value": "555-1" } ],
              "address": [ { "city": "PleasantVille" } ],
              "contact": [ { "name": { "family": "du Marche" } } ] }
            """;

        var html = await new FhirRenderer([new VendorTemplates()]).RenderViewAsync(patient, "view");

        Assert.That(html, Does.Not.Contain("fx-notice").And.Not.Contain("fx-other"));
    }

    [Test]
    public async System.Threading.Tasks.Task WhatTheVendorObservationDoesNotShow_IsListed()
    {
        const string observation = """
            { "resourceType": "Observation", "id": "o", "status": "final", "code": { "text": "Pulse" },
              "identifier": [ { "system": "urn:lab", "value": "L-42" } ],
              "extension": [ { "url": "http://example.org/StructureDefinition/lab-flag", "valueString": "rerun" } ] }
            """;

        var html = await new FhirRenderer([new VendorTemplates()]).RenderViewAsync(observation, "view");

        Assert.That(html, Does.Contain("Not shown above: 2 elements (identifier, extension)").And.Contain("L-42").And.Contain("rerun").And.Contain("lab-flag"));
    }

    [Test]
    public async System.Threading.Tasks.Task APlainRender_DoesNotAddAnything()
    {
        var html = await Renderer(Basic()).RenderAsync(Patient, "view");

        Assert.That(html, Is.EqualTo("Chalmers|male"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheBlocks_SurviveTheWhiteListOfTheSanitizer()
    {
        var html = await ViewAsync(Patient, Basic());

        var clean = FxHtml.Sanitize(html);

        Assert.That(clean, Does.Contain("""<div class="fx-notice fx-notice-warning">""").And.Contain("""<details class="fx-other"><summary>"""));
    }
}
