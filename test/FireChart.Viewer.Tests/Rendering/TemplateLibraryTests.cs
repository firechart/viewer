using FireChart.Viewer.Core;
using FireChart.Viewer.Library;

namespace FireChart.Viewer.Tests.Rendering;

// What the administration relies on: syntax checks, the identity of a template text, the shipped library and samples
public class TemplateLibraryTests
{
    [Test]
    public void Validate_ReturnsNullForATemplateThatParses_AndThePositionOfAMistake()
    {
        Assert.That(FhirRenderer.Validate("<p>{{ self.name | join: ', ' }}</p>{% apply self.contact %}"), Is.Null);
        Assert.That(FhirRenderer.Validate("a\n{% if %}"), Does.Contain("(2:"));
    }

    [Test]
    public void TemplateHash_IgnoresLineEndings_ButNotTheText()
    {
        Assert.That(TemplateHash.Of("a\r\nb"), Is.EqualTo(TemplateHash.Of("a\nb")));
        Assert.That(TemplateHash.Of("a"), Is.Not.EqualTo(TemplateHash.Of("b")));
    }

    [Test]
    public void TheVendorLibrary_ListsItsKeys_AndEveryTemplateParses()
    {
        var vendor = TestLibrary.Vendor;

        Assert.That(vendor.Keys, Is.SupersetOf(new[] { "Patient", "HumanName", "Patient.contact", "_primitive", "_complex", "row" }));
        foreach (var key in vendor.Keys)
        {
            Assert.That(FhirRenderer.Validate(vendor.Find(key)!), Is.Null, key);
        }
    }

    [Test]
    public void TheSamples_AreFoundByName()
    {
        var samples = TemplateSamples.ReadFolder(TestLibrary.WebRoot());

        Assert.That(samples.Names, Is.SupersetOf(new[] { "Patient-full", "Patient-minimal" }));
        Assert.That(samples.Find("Patient-full"), Does.Contain("\"resourceType\": \"Patient\""));
        Assert.That(samples.Find("nothing"), Is.Null);
    }
}
