using System.Text.RegularExpressions;
using FireChart.Viewer.Core;
using FireChart.Viewer.Library;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace FireChart.Viewer.Tests.Rendering;

// The Liquid templates of FHIR resources: dispatch by path and type, client overrides over the vendor library
public class RenderingTests
{
    private static readonly VendorTemplates s_vendor = TestLibrary.Vendor;

    private static Patient SamplePatient(string family = "Chalmers")
    {
        return new FhirJsonDeserializer().Deserialize<Patient>(
        $$"""
        {
          "resourceType": "Patient",
          "gender": "male",
          "birthDate": "1974-12-25",
          "name": [ { "family": "{{family}}", "given": [ "Peter", "James" ] } ],
          "contact": [ { "name": { "family": "du Marche", "given": [ "Benedicte" ] } } ]
        }
        """);
    }

    // The vendor templates may be formatted (line breaks, indentation, CRLF): the tests compare the markup without the white space around the tags
    private static string Flat(string html)
    {
        return Regex.Replace(Regex.Replace(html, @"\s+", " "), @"\s*([<>])\s*", "$1");
    }

    private static MemoryTemplateSource Override(params KeyValuePair<string, string>[] templates)
    {
        return new MemoryTemplateSource(templates.ToDictionary(t => t.Key, t => t.Value));
    }

    private static KeyValuePair<string, string> Tpl(string key, string text)
    {
        return new(key, text);
    }

    [Test]
    public async System.Threading.Tasks.Task Render_DispatchesTheElementsToTheirTemplates()
    {
        var html = await new FhirRenderer([s_vendor]).RenderAsync(SamplePatient());

        Assert.That(Flat(html), Does.Contain("<article class=\"fhir-patient\">"));
        Assert.That(Flat(html), Does.Contain("<span class=\"human-name\">Peter James Chalmers</span>"));
        Assert.That(Flat(html), Does.Contain("<section class=\"contact\"><span class=\"human-name\">Benedicte du Marche</span></section>"));
        Assert.That(Flat(html), Does.Contain("<span class=\"label\">Gender</span><span class=\"value\">male</span>"));
        Assert.That(Flat(html), Does.Contain("1974-12-25"));
    }

    [Test]
    public async System.Threading.Tasks.Task Render_EncodesTheDataButNotTheTemplate()
    {
        var html = await new FhirRenderer([s_vendor]).RenderAsync(SamplePatient("<b>x</b>"));

        Assert.That(Flat(html), Does.Contain("&lt;b&gt;x&lt;/b&gt;"));
        Assert.That(Flat(html), Does.Not.Contain("<b>x</b>"));
    }

    [Test]
    public async System.Threading.Tasks.Task AnOverrideOfADataType_AppliesEverywhereTheTypeIsShown()
    {
        var overrides = Override(Tpl("HumanName", "<i>{{ self.family }}</i>"));

        var html = await new FhirRenderer([overrides, s_vendor]).RenderAsync(SamplePatient());

        Assert.That(Flat(html), Does.Contain("<h2><i>Chalmers</i></h2>"));
        Assert.That(Flat(html), Does.Contain("<section class=\"contact\"><i>"));
    }

    [Test]
    public async System.Threading.Tasks.Task ATemplateOfAPath_BeatsTheTemplateOfTheType()
    {
        var overrides = Override(Tpl("Patient.name", "<b>{{ self.family }}</b>"));

        var html = await new FhirRenderer([overrides, s_vendor]).RenderAsync(SamplePatient());

        Assert.That(Flat(html), Does.Contain("<h2><b>Chalmers</b></h2>"));
        Assert.That(Flat(html), Does.Contain("<section class=\"contact\"><span class=\"human-name\">"), "the contact name is not Patient.name");
    }

    [Test]
    public async System.Threading.Tasks.Task AnOverrideThatDoesNotParse_FallsBackToTheVendorTemplate_AndIsReported()
    {
        var problems = new List<string>();
        var overrides = Override(Tpl("HumanName", "{% if %}broken"));

        var html = await new FhirRenderer([overrides, s_vendor], problems.Add).RenderAsync(SamplePatient());

        Assert.That(Flat(html), Does.Contain("<span class=\"human-name\">Peter James Chalmers</span>"));
        Assert.That(problems, Has.Count.EqualTo(1));
        Assert.That(problems[0], Does.Contain("HumanName"));
    }

    [Test]
    public async System.Threading.Tasks.Task AnElementWithoutATemplate_IsShownByTheGenericOne()
    {
        var html = await new FhirRenderer([s_vendor]).RenderAsync(new Patient { Active = true, Address = [new Address { City = "Boston" }] });

        Assert.That(Flat(html), Does.Contain("<h2></h2>"));
        Assert.That(Flat(html), Does.Not.Contain("Boston"), "the Patient template does not ask for the address");
    }

    [Test]
    public async System.Threading.Tasks.Task ATemplateThatApplies_ItselfForever_StopsAtTheDepthLimit()
    {
        var overrides = Override(Tpl("Patient", "{% apply self.contact %}"), Tpl("Patient.contact", "{% apply self %}"));

        var html = await new FhirRenderer([overrides]).RenderAsync(SamplePatient());

        Assert.That(html, Does.Contain("fx-notice-danger").And.Contain("Template error").And.Contain("deeper"), "a template that fails is shown as an error, not hidden");
    }

    [Test]
    public async System.Threading.Tasks.Task TheModeOfTheRender_PrefersTheTemplatesOfTheMode_ForTheWholeDocument()
    {
        var renderer = new FhirRenderer([s_vendor]);

        var plain = await renderer.RenderAsync(SamplePatient());
        var familyFirst = await renderer.RenderAsync(SamplePatient(), "family-first");

        Assert.That(Flat(plain), Does.Contain("Peter James Chalmers").And.Contain("Benedicte du Marche"));
        Assert.That(Flat(familyFirst), Does.Contain("Chalmers Peter James").And.Contain("du Marche Benedicte"), "the contact name is inside another template");
    }

    [Test]
    public async System.Threading.Tasks.Task AModeThatATypeDoesNotHave_FallsBackToThePlainTemplate()
    {
        var html = await new FhirRenderer([s_vendor]).RenderAsync(SamplePatient(), "no-such-mode");

        Assert.That(Flat(html), Does.Contain("Peter James Chalmers"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheModeOfTheApplyTag_AppliesToThatElementOnly()
    {
        var overrides = Override(Tpl("Patient", "{% apply self.name mode: \"family-first\" %}|{% apply self.name %}"));

        var html = await new FhirRenderer([overrides, s_vendor]).RenderAsync(SamplePatient());

        Assert.That(Flat(html), Does.Contain("Chalmers Peter James</span>|<span class=\"human-name\">Peter James Chalmers"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheModeOfTheApplyTag_BeatsTheModeOfTheRender()
    {
        var overrides = Override(Tpl("Patient", "{% apply self.name mode: \"plain\" %}"), Tpl("HumanName@plain", "P"), Tpl("HumanName@other", "O"));

        var html = await new FhirRenderer([overrides, s_vendor]).RenderAsync(SamplePatient(), "other");

        Assert.That(Flat(html), Is.EqualTo("P"));
    }

    [Test]
    public async System.Threading.Tasks.Task AnOverrideOfAModeTemplate_BeatsTheVendorTemplateOfTheMode()
    {
        var overrides = Override(Tpl("HumanName@family-first", "<i>{{ self.family }}</i>"));

        var html = await new FhirRenderer([overrides, s_vendor]).RenderAsync(SamplePatient(), "family-first");

        Assert.That(Flat(html), Does.Contain("<h2><i>Chalmers</i></h2>"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheTileOfAPatient_IsAListRow_WithTheNameTheBirthDateTheIdAndTheBadges()
    {
        var patient = SamplePatient();
        patient.Id = "p-1";

        var tile = await new FhirRenderer([s_vendor]).RenderTileAsync(patient);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Html, Does.StartWith("<li class=\"fx-list-row\">").And.Contain("fx-badge").And.Contain("fx-tile-title"));
            Assert.That(tile.Text, Does.Contain("Peter James Chalmers").And.Contain("1974-12-25").And.Contain("p-1").And.Contain("male"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task TheTile_KeepsQuotesAndAmpersandsOfTheRecordAsText()
    {
        var patient = SamplePatient();
        patient.Name[0].Family = "O'Brien & \"Co\" <b>";
        patient.Id = "p-1";

        var tile = await new FhirRenderer([s_vendor]).RenderTileAsync(patient);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Text, Does.Contain("Peter James O'Brien & \"Co\" <b>"));
            Assert.That(tile.Html, Does.Contain("&lt;b&gt;").And.Not.Contain("<b>"), "the value of the record is not markup");
        });
    }

    [Test]
    public async System.Threading.Tasks.Task ATypeWithoutATileTemplate_ShowsTheIdAndTheType_WithoutAProblem()
    {
        var patient = SamplePatient();
        patient.Id = "p-1";
        var problems = new List<string>();

        var tile = await new FhirRenderer([Override()], problems.Add).RenderTileAsync(patient);

        Assert.That(tile.Text, Is.EqualTo("p-1 Patient"));
        Assert.That(tile.Html, Does.StartWith("<li class=\"fx-list-row\">"));
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public async System.Threading.Tasks.Task ATileTemplate_FillsSlots_TheFrameLaysOutTheRow_AndWhatIsOutsideTheSlotsIsDropped()
    {
        var overrides = Override(Tpl("Patient@tile", "outside{% slot \"title\" %}{{ self.id }}{% endslot %}{% slot \"trailing\" %}<span class=\"fx-badge fx-badge-info\">51 y</span>{% endslot %}"));

        var tile = await new FhirRenderer([overrides, s_vendor]).RenderTileAsync("""{ "resourceType": "Patient", "id": "p-1" }""");

        Assert.Multiple(() =>
        {
            Assert.That(tile.Html, Is.EqualTo("<li class=\"fx-list-row\"><div class=\"fx-list-col-grow fx-tile\"><div class=\"fx-tile-main\"><div class=\"fx-tile-title\">p-1</div></div>" +
                                              "<div class=\"fx-tile-end\"><span class=\"fx-badge fx-badge-info\">51 y</span></div></div></li>"));
            Assert.That(tile.Text, Is.EqualTo("p-1 51 y"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task ATileTemplateWithoutATitleSlot_ShowsTheIdAndTheType()
    {
        var overrides = Override(Tpl("Patient@tile", "<div>x</div>{% slot \"subtitle\" %}s{% endslot %}"));

        var tile = await new FhirRenderer([overrides, s_vendor]).RenderTileAsync("""{ "resourceType": "Patient", "id": "p-1" }""");

        Assert.That(tile.Text, Is.EqualTo("p-1 Patient"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheOptionsOfTheRows_ShowTheStatusAndTheAge_WithoutRenderingTheTemplateAgain()
    {
        var overrides = Override(Tpl("Patient@tile", "{% slot \"title\" %}T{% endslot %}{% slot \"status\" %}S{% endslot %}"));

        var tile = await new FhirRenderer([overrides, s_vendor]).RenderTileAsync("""{ "resourceType": "Patient", "id": "p-1" }""");

        Assert.Multiple(() =>
        {
            Assert.That(tile.Text, Is.EqualTo("T S"), "the status is shown by default");
            Assert.That(tile.WithOptions(new TileOptions(ShowStatus: false)).Text, Is.EqualTo("T"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task ATileTemplate_CannotBringScriptsLinksOrImages_ButTheirTextStays()
    {
        const string template = "{% slot \"title\" %}<script>alert(1)</script><a href=\"javascript:x\" onclick=\"x()\">link</a><img src=x onerror=y><b onclick=\"x()\" style=\"color:red\" class=\"evil fx-ok\">bold</b>{% endslot %}";
        var overrides = Override(Tpl("Patient@tile", template));

        var tile = await new FhirRenderer([overrides, s_vendor]).RenderTileAsync("""{ "resourceType": "Patient", "id": "p-1" }""");

        Assert.Multiple(() =>
        {
            Assert.That(tile.Html, Does.Contain("<div class=\"fx-tile-title\">link<b class=\"fx-ok\">bold</b></div>"));
            Assert.That(tile.Text, Is.EqualTo("linkbold"));
        });
    }

    [TestCase("a < b & c", "a &lt; b &amp; c")]
    [TestCase("<b>not bold</b>", "&lt;b&gt;not bold&lt;/b&gt;")]
    [TestCase("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    public async System.Threading.Tasks.Task TheValuesOfARecord_AreText_NotMarkup(string family, string encoded)
    {
        var overrides = Override(Tpl("Patient@tile", "{% slot \"title\" %}{{ self.name[0].family }}{% endslot %}"));
        var json = $$"""{ "resourceType": "Patient", "id": "p-1", "name": [ { "family": {{System.Text.Json.JsonSerializer.Serialize(family)}} } ] }""";

        var tile = await new FhirRenderer([overrides, s_vendor]).RenderTileAsync(json);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Text, Is.EqualTo(family), "the text of the record is shown as it is");
            Assert.That(tile.Html, Does.Contain($"<div class=\"fx-tile-title\">{encoded}</div>"), "and it is not markup");
        });
    }

    [Test]
    public async System.Threading.Tasks.Task ATileTemplateThatDoesNotParse_ShowsTheErrorOnTheRow_AndReportsAProblem()
    {
        var patient = SamplePatient();
        patient.Id = "p-1";
        var problems = new List<string>();
        var overrides = Override(Tpl("Patient@tile", "{% if %}broken"));

        var tile = await new FhirRenderer([overrides, s_vendor], problems.Add).RenderTileAsync(patient);

        Assert.Multiple(() =>
        {
            Assert.That(tile.Text, Does.StartWith("p-1 Template error:").And.Contain("Patient@tile"), "the error is on the row, not only in the log");
            Assert.That(tile.Html, Does.Contain("fx-tile-error"));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("Patient@tile"));
        });
    }

    [Test]
    public async System.Threading.Tasks.Task AnOverrideOfTheTileTemplate_ChoosesTheContentOfTheRow()
    {
        var patient = SamplePatient();
        patient.Id = "p-1";
        var overrides = Override(Tpl("Patient@tile", "{% slot \"title\" %}{{ self.id }}{% endslot %}{% slot \"subtitle\" %}{{ self.gender }}{% endslot %}"));

        var tile = await new FhirRenderer([overrides, s_vendor]).RenderTileAsync(patient);

        Assert.That(tile.Text, Is.EqualTo("p-1 male"));
    }

    [Test]
    public async System.Threading.Tasks.Task AChainOfModes_PrefersTheStrongestModeThatHasATemplate()
    {
        var overrides = Override(Tpl("HumanName@a", "A"), Tpl("HumanName@b", "B"));
        var renderer = new FhirRenderer([overrides, s_vendor]);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(Flat(await renderer.RenderAsync(SamplePatient(), "a,b")), Does.Contain("<h2>A</h2>"));
            Assert.That(Flat(await renderer.RenderAsync(SamplePatient(), "b,a")), Does.Contain("<h2>B</h2>"));
            Assert.That(Flat(await renderer.RenderAsync(SamplePatient(), "x, b")), Does.Contain("<h2>B</h2>"), "a mode without a template is skipped");
        });
    }

    [Test]
    public async System.Threading.Tasks.Task TheCurrentMode_StandsForTheChainInForce()
    {
        var overrides = Override(Tpl("Patient", "{% apply self.name mode: \"#current,b\" %}|{% apply self.name mode: \"b,#current\" %}"), Tpl("HumanName@a", "A"), Tpl("HumanName@b", "B"));

        var html = await new FhirRenderer([overrides, s_vendor]).RenderAsync(SamplePatient(), "a");

        Assert.That(html, Is.EqualTo("A|B"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheModeOfTheTile_AddsModesAfterTile()
    {
        var patient = SamplePatient();
        patient.Id = "p-1";
        var overrides = Override(Tpl("Patient@tile", "{% slot \"title\" %}{% apply self.name mode: \"text,#current\" %}{% endslot %}"), Tpl("HumanName@text", "T"));

        var tile = await new FhirRenderer([overrides, s_vendor]).RenderTileAsync(patient, "family-first");

        Assert.That(tile.Text, Is.EqualTo("T"));
    }

    [Test]
    public async System.Threading.Tasks.Task TheTileOfAResourceGivenAsJson_NeedsNoPoco_AndLeavesUnknownElementsAlone()
    {
        const string json = """{ "resourceType": "Patient", "id": "p-2", "birthDate": "1980-01-02", "name": [ { "family": "Smith", "given": [ "Ann" ] } ], "somethingOfALaterRelease": { "x": 1 } }""";

        var tile = await new FhirRenderer([s_vendor]).RenderTileAsync(json);

        Assert.That(tile.Text, Does.Contain("Ann Smith").And.Contain("1980-01-02").And.Contain("p-2"));
    }
}
