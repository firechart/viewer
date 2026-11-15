using FireChart.Viewer.Components;
using FireChart.Viewer.Core.Fhir.Metadata;

namespace FireChart.Viewer.Tests.Client;

// The colours of the resource types: every type of the releases STU3 to R6 in one group, a colour of Apple per group, the variables of fx.css
public class ResourceTypeColorsTests
{
    private static readonly string[] s_apple = ["red", "orange", "yellow", "green", "mint", "teal", "cyan", "blue", "indigo", "purple", "pink", "brown"];

    [Test]
    public void EveryTypeOfTheMetadata_HasTheColourOfItsGroup_AVariableOfFxCss()
    {
        var known = FhirMetadata.Releases.SelectMany(release => FhirMetadata.Of(release)!.ResourceTypes).Distinct(StringComparer.Ordinal).ToList();
        var style = TestLibrary.Styles.Vendor;

        Assert.Multiple(() =>
        {
            Assert.That(known.Where(type => ResourceTypeColors.Of(type) is null), Is.Empty, "a type of FHIR without a colour");
            Assert.That(ResourceTypeColors.Types, Is.Ordered.Using<string>(StringComparer.Ordinal));
            foreach (var color in s_apple)
            {
                Assert.That(style, Does.Contain($"--fx-type-{color}:"), color);
            }

            Assert.That(ResourceTypeColors.Types.Select(ResourceTypeColors.Of).Distinct(), Is.EquivalentTo(s_apple.Select(color => $"var(--fx-type-{color})")));
        });
    }

    [Test]
    public void ATypeHasTheColourOfWhatItIsAbout()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ResourceTypeColors.Of("Patient"), Is.EqualTo("var(--fx-type-blue)"));
            Assert.That(ResourceTypeColors.Of("Condition"), Is.EqualTo("var(--fx-type-red)"));
            Assert.That(ResourceTypeColors.Of("Observation"), Is.EqualTo("var(--fx-type-mint)"));
            Assert.That(ResourceTypeColors.Of("StructureDefinition"), Is.EqualTo("var(--fx-type-indigo)"));
            Assert.That(ResourceTypeColors.Of("patient"), Is.EqualTo(ResourceTypeColors.Of("Patient")), "the case does not matter");
        });
    }

    [TestCase("")]
    [TestCase("NoSuchType")]
    [TestCase(null)]
    public void AnUnknownType_HasNoColour(string? type)
    {
        Assert.That(ResourceTypeColors.Of(type), Is.Null);
    }
}
