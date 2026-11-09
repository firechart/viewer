using FireChart.Viewer.Components;

namespace FireChart.Viewer.Tests.Client;

// The rainbow of the resource types: every type of R6 has its colour, from red (the first) to violet (the last)
public class ResourceTypeColorsTests
{
    [Test]
    public void TheTypes_AreAlphabetical_AndEveryOneHasItsOwnColour()
    {
        var colors = ResourceTypeColors.Types.Select(type => ResourceTypeColors.Of(type)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ResourceTypeColors.Types, Is.Ordered.Using<string>(StringComparer.Ordinal));
            Assert.That(colors, Has.None.Null);
            Assert.That(colors.Distinct().Count(), Is.EqualTo(ResourceTypeColors.Types.Count));
        });
    }

    [Test]
    public void TheRainbow_RunsFromRedToViolet()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ResourceTypeColors.Of("Account"), Is.EqualTo("oklch(0.62 0.1 25)"));
            Assert.That(ResourceTypeColors.Of("VisionPrescription"), Is.EqualTo("oklch(0.62 0.1 305)"));
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
