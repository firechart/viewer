using System.Text.Json;
using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Tests.Viewer;

// The files of the Viewer: reading and writing again gives the same text, and reading is strict (.tasks/viewer/viewer.md, 4.2)
public class ViewerJsonTests
{
    [Test]
    public void ASearchForm_WrittenAndReadAgain_IsTheSameText_WithItsLettersKeysAndEnumerationsAsTheyAre()
    {
        var form = new SearchFormDefinition { Name = "default", AuthoredForVersion = "4.0.1" };
        form.Title.SetDefault("Find patients");
        form.Title["ru-RU"] = "Поиск пациентов";
        form.Groups.Add(new SearchFormGroup { Code = "dates", Order = 2, CollapsedByDefault = true });
        var gender = new SearchFormField { QueryName = "gender", ParameterType = SearchParameterType.Token, Control = SearchFieldControl.MultiSelect, Included = false };
        var female = new SearchFormOption { Code = "female", System = "http://hl7.org/fhir/administrative-gender" };
        female.Display["ru"] = "Женский";
        gender.Options.Add(female);
        gender.Lookup = new ReferenceLookup { MinChars = 3 };
        form.Fields.Add(gender);

        var text = ViewerJson.Write(form);
        var again = ViewerJson.Write(ViewerJson.Read<SearchFormDefinition>(text));

        Assert.Multiple(() =>
        {
            Assert.That(again, Is.EqualTo(text));
            Assert.That(text, Does.Contain("\"ru-RU\": \"Поиск пациентов\"").And.Contain("\"control\": \"multiSelect\"").And.Contain("\"included\": false"));
            Assert.That(text, Does.Not.Contain("\r").And.Not.Contain("null"));
            Assert.That(text, Does.EndWith("}\n"));
        });
    }

    [Test]
    public void AValueThatDiffersFromItsInitializer_IsNotLost()
    {
        var form = new ClinicalSummaryFormDefinition { Code = "default", ShowStatus = false };
        form.Sections.Add(new ClinicalSummaryFormSection { Code = "problems", Visible = false, HideIfUnsupported = false });

        var read = ViewerJson.Read<ClinicalSummaryFormDefinition>(ViewerJson.Write(form));

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
        var error = Assert.Throws<JsonException>(() => ViewerJson.Read<ClinicalSummaryFormDefinition>(json));

        Assert.That(error!.Message, Does.Contain(named));
    }
}
