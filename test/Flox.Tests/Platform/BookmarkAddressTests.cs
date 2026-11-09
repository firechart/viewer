using Flox.Core.Models;

namespace Flox.Tests.Platform;

// A bookmark is a place (/Patient/123), never a search (/Patient?name=John)
public class BookmarkAddressTests
{
    private static bool Accepts(string url)
    {
        return new UserBookmarkValidator().Validate(new UserBookmark { Url = url }).IsValid;
    }

    [TestCase("/server/5/resources/Patient/123")]
    [TestCase("https://example.org/Patient/123")]
    [TestCase("/conformance/hl7.fhir.r4.core@4.0.1/StructureDefinition/Patient")]
    [TestCase("/page#section")]
    public void APlace_IsAccepted(string url)
    {
        Assert.That(Accepts(url), Is.True);
    }

    [TestCase("/server/5/resources/Patient?name=John")]
    [TestCase("/Patient?name=John&birthdate=1980-01-01")]
    [TestCase("https://example.org/Patient/123?_format=json")]
    [TestCase("/server/5/resources/Patient?")]
    public void AnAddressWithAQueryString_IsRefused(string url)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Accepts(url), Is.False);
            Assert.That(UserBookmarkValidator.HasQueryString(url), Is.True);
        });
    }
}
