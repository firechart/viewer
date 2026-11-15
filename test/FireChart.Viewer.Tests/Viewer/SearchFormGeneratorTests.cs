using FireChart.Viewer.Core;
using FireChart.Viewer.Core.Fhir.Metadata;
using FireChart.Viewer.Core.Forms;

namespace FireChart.Viewer.Tests.Viewer;

// The form a type has when nobody designed one: made from the metadata of the release, with no server and no package
public class SearchFormGeneratorTests
{
    private static IEnumerable<string> Releases()
    {
        return ["r3", "r4", "r4b", "r5"];
    }

    [Test]
    public void Every_resource_type_of_every_release_has_a_form_that_the_Viewer_reads([ValueSource(nameof(Releases))] string release)
    {
        var metadata = FhirMetadata.Of(SearchFormGenerator.ReleaseOf(release))!;
        var empty = new List<string>();
        foreach (var type in metadata.ResourceTypes)
        {
            var form = SearchFormGenerator.Generate(release, type)!;
            var again = ViewerJson.Read<SearchFormDefinition>(ViewerJson.Write(form));
            if (again.Fields.Count == 0)
            {
                empty.Add(type);
            }
        }

        Assert.That(metadata.ResourceTypes, Has.Count.GreaterThan(100));
        Assert.That(empty, Is.Empty, "a type without a field");
    }

    [Test]
    public void The_fields_people_look_for_come_first_and_the_parameters_of_all_resources_are_out_of_the_form()
    {
        var form = SearchFormGenerator.Generate("R4", "Patient")!;
        var included = form.Fields.Where(f => !f.Hidden).Select(f => f.QueryName).ToList();
        var excluded = form.Fields.Where(f => f.Hidden).Select(f => f.QueryName).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(included.Take(6), Is.EqualTo(new[] { "identifier", "name", "family", "given", "birthdate", "gender" }));
            Assert.That(excluded, Does.Contain("_id").And.Contain("_lastUpdated"));
            Assert.That(form.Fields.Select(f => f.Order), Is.Ordered);
            Assert.That(form.IsGenerated, Is.True);
            Assert.That(form.Fields.Select(f => f.ParameterType), Has.None.EqualTo(SearchParameterType.Composite).And.None.EqualTo(SearchParameterType.Special));
        });
    }

    [Test]
    public void The_control_follows_the_parameter_and_the_binding_of_its_element()
    {
        var patient = SearchFormGenerator.Generate("R4", "Patient")!.Fields.ToDictionary(f => f.QueryName);
        var observation = SearchFormGenerator.Generate("R4", "Observation")!.Fields.ToDictionary(f => f.QueryName);

        Assert.Multiple(() =>
        {
            Assert.That(patient["gender"].Control, Is.EqualTo(SearchFieldControl.Select), "a required binding");
            Assert.That(patient["gender"].ValueSetUrl, Is.EqualTo("http://hl7.org/fhir/ValueSet/administrative-gender"));
            Assert.That(patient["active"].Control, Is.EqualTo(SearchFieldControl.Boolean));
            Assert.That(patient["birthdate"].Control, Is.EqualTo(SearchFieldControl.DateRange));
            Assert.That(patient["general-practitioner"].Lookup?.TargetTypes, Does.Contain("Practitioner"));
            Assert.That(patient["gender"].Hint.Resolve("en-US"), Is.EqualTo("Gender of the patient"));
            Assert.That(observation["code"].Control, Is.EqualTo(SearchFieldControl.Autocomplete), "an example binding: suggested, typed");
            Assert.That(observation["code"].AllowFreeText, Is.True);
            Assert.That(observation["value-quantity"].Units.Select(u => u.Code), Does.Contain("mm[Hg]"));
            Assert.That(observation["date"].Comparators, Does.Contain("ge"));
        });
    }

    [Test]
    public void A_datatype_an_unknown_type_and_a_release_without_metadata_have_no_form()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SearchFormGenerator.Generate("R4", "HumanName"), Is.Null);
            Assert.That(SearchFormGenerator.Generate("R4", "DomainResource"), Is.Null);
            Assert.That(SearchFormGenerator.Generate("R4", "NoSuchType"), Is.Null);
            Assert.That(SearchFormGenerator.Generate("R6", "Patient"), Is.Null);
            Assert.That(SearchFormGenerator.Generate("r3", "Patient")?.FhirRelease, Is.EqualTo("STU3"), "r3 of the library is STU3");
        });
    }
}
