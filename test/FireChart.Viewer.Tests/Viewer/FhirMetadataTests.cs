using FireChart.Viewer.Core.Fhir.Metadata;

namespace FireChart.Viewer.Tests.Viewer;

// What the Firely SDK lacks for the search forms, generated from the core packages: the bindings of the elements and the search parameters
public class FhirMetadataTests
{
    [Test]
    public void The_bindings_of_the_elements_are_known_in_every_release_with_their_strength()
    {
        Assert.Multiple(() =>
        {
            foreach (var release in FhirMetadata.Releases)
            {
                var metadata = FhirMetadata.Of(release)!;
                Assert.That(metadata.BindingOf("Patient.gender")?.ValueSetUrl, Is.EqualTo("http://hl7.org/fhir/ValueSet/administrative-gender"), release);
                Assert.That(metadata.BindingOf("Observation.status")?.Strength, Is.EqualTo(BindingStrength.Required), release);
                Assert.That(metadata.BindingOf("Observation.category")?.ValueSetUrl, Is.EqualTo("http://hl7.org/fhir/ValueSet/observation-category"), release);
            }

            Assert.That(FhirMetadata.Of("R4")!.BindingOf("Observation.code")?.Strength, Is.EqualTo(BindingStrength.Example), "LOINC: an example");
            Assert.That(FhirMetadata.Of("R4")!.BindingOf("Observation.status")?.ValueSetVersion, Is.EqualTo("4.0.1"));
            Assert.That(FhirMetadata.Of("STU3")!.BindingOf("Observation.status")?.ValueSetVersion, Is.Null, "STU3 names no version");
            Assert.That(FhirMetadata.Of("r4")!.BindingOf("Patient.name"), Is.Null, "an element without a binding");
        });
    }

    [Test]
    public void A_search_parameter_has_its_comparators_and_targets_and_one_of_all_resources_is_found_by_any_type()
    {
        var r4 = FhirMetadata.Of("R4")!;

        Assert.Multiple(() =>
        {
            Assert.That(r4.SearchParameterOf("Observation", "date")?.Comparators, Does.Contain("ge").And.Contain("le"));
            Assert.That(r4.SearchParameterOf("Observation", "subject")?.Targets, Does.Contain("Patient").And.Contain("Group"));
            Assert.That(r4.SearchParameterOf("Patient", "_id")?.ResourceType, Is.EqualTo("Resource"));
            Assert.That(r4.SearchParameterOf("Patient", "no-such-parameter"), Is.Null);
            Assert.That(FhirMetadata.Of("R6"), Is.Null, "a ballot has no metadata");
            Assert.That(FhirMetadata.Of(null)!.Release, Is.EqualTo("R4"), "no release is R4, as for the models");
        });
    }
}
