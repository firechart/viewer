using Flox.Shared.Branding;

namespace Flox.Tests.Branding;

// The licenses of the packages are read at the first use, not at the start of every client; the list must still come from the resources
public class PlatformBrandingTests
{
    [Test]
    public void Licenses_AreReadFromTheResources_WhenFirstUsed()
    {
        var licenses = FloxPlatformBranding.Default.Licenses;

        Assert.That(licenses, Has.Count.GreaterThan(100));
        Assert.That(licenses.Select(license => license.PackageId), Is.Ordered);
    }
}
