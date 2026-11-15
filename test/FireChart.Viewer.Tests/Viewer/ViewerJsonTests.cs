using System.Text.Json;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Tests.Viewer;

// The files of the Viewer: reading and writing again gives the same text, and reading is strict
public class ViewerJsonTests
{
    [Test]
    public void ASearchForm_WrittenAndReadAgain_IsTheSameText_WithItsLettersKeysAndEnumerationsAsTheyAre()
    {
        var form = new SearchFormDefinition { Name = "default", FhirRelease = "R4" };
        form.Title.SetDefault("Find patients");
        form.Title["ru-RU"] = "Поиск пациентов";
        form.Groups.Add(new SearchFormGroup { Code = "dates", Order = 2, CollapsedByDefault = true });
        var gender = new SearchFormField { QueryName = "gender", ParameterType = SearchParameterType.Token, Control = SearchFieldControl.MultiSelect, Hidden = true };
        gender.ValueSetUrl = "http://hl7.org/fhir/ValueSet/administrative-gender";
        gender.Lookup = new ReferenceLookup { MinChars = 3 };
        form.Fields.Add(gender);

        var text = ViewerJson.Write(form);
        var again = ViewerJson.Write(ViewerJson.Read<SearchFormDefinition>(text));

        Assert.Multiple(() =>
        {
            Assert.That(again, Is.EqualTo(text));
            Assert.That(text, Does.Contain("\"ru-RU\": \"Поиск пациентов\"").And.Contain("\"control\": \"multiSelect\"").And.Contain("\"hidden\": true"));
            Assert.That(text, Does.Not.Contain("\r").And.Not.Contain("null"));
            Assert.That(text, Does.EndWith("}\n"));
        });
    }

    [Test]
    public void AValueThatDiffersFromItsInitializer_IsNotLost()
    {
        var form = new SummaryFormDefinition { Code = "default", ShowStatus = false };
        form.Sections.Add(new SummaryFormSection { Code = "problems", Visible = false, HideIfUnsupported = false });

        var read = ViewerJson.Read<SummaryFormDefinition>(ViewerJson.Write(form));

        Assert.Multiple(() =>
        {
            Assert.That(read.ShowStatus, Is.False);
            Assert.That(read.Sections[0].Visible, Is.False);
            Assert.That(read.Sections[0].HideIfUnsupported, Is.False);
        });
    }

    [TestCase("""{ "code": "default", "sectoins": [] }""", "sectoins")]
    [TestCase("""{ "code": null }""", "code")]
    [TestCase("""{ "showAge": true }""", "code")]
    public void AFileThatBreaksARule_IsRefused_WithThePlaceNamed(string json, string named)
    {
        var error = Assert.Throws<JsonException>(() => ViewerJson.Read<SummaryFormDefinition>(json));

        Assert.That(error!.Message, Does.Contain(named));
    }

    [Test]
    public void AFormOfVersion1_ReadsAsTiles_AndTheOptionsOfTheTilesAreReadByTheirClass()
    {
        var form = ViewerJson.Read<SummaryFormDefinition>("""
            { "formatVersion": 1, "code": "old", "sections": [ { "code": "s", "searches": [ { "resourceType": "Condition" } ] } ] }
            """);
        var options = SummaryComponents.ReadTilesOptions(System.Text.Json.Nodes.JsonNode.Parse("""{ "showAge": true }""")!.AsObject());

        Assert.Multiple(() =>
        {
            Assert.That(form.Sections[0].Component, Is.EqualTo(SummaryComponents.Tiles));
            Assert.That(form.Sections[0].HideIfEmpty, Is.False);
            Assert.That(options.ShowAge, Is.True);
            Assert.That(() => SummaryComponents.ReadTilesOptions(System.Text.Json.Nodes.JsonNode.Parse("""{ "showAgee": true }""")!.AsObject()), Throws.InstanceOf<System.Text.Json.JsonException>());
        });
    }

    [Test]
    public void AFormOfVersion2_IsOfAPatient_AndItsPatientParamIsReadAsTheContextParam_NeverWritten()
    {
        var form = ViewerJson.Read<SummaryFormDefinition>("""
            { "formatVersion": 2, "code": "old", "sections": [ { "code": "s", "searches": [ { "resourceType": "Coverage", "patientParam": "beneficiary" }, { "resourceType": "Condition" } ] } ] }
            """);
        var written = ViewerJson.Write(form);

        Assert.Multiple(() =>
        {
            Assert.That(form.Context, Is.EqualTo(SummaryContexts.Patient));
            Assert.That(form.Sections[0].Searches[0].ParamOf(form.Context), Is.EqualTo("beneficiary"));
            Assert.That(form.Sections[0].Searches[1].ParamOf(SummaryContexts.Encounter), Is.EqualTo("encounter"), "none named: the one of the context");
            Assert.That(written, Does.Contain("\"contextParam\": \"beneficiary\"").And.Not.Contain("patientParam"));
        });
    }
}
