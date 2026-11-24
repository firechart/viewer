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
}
