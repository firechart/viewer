using FireChart.Viewer.Routing;
using Microsoft.AspNetCore.Components;

namespace FireChart.Viewer.Tests.Viewer;

// The experimental routing of the Viewer: the table of its addresses, and its address below the prefix of the host through the NavigationManager
public class ViewerRoutingTests
{
    private sealed class FakeNavigation : NavigationManager
    {
        public FakeNavigation(string uri)
        {
            Initialize("https://app.example.test/app/", uri);
        }

        public List<string> Visited { get; } = [];

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Visited.Add(uri);
            Uri = ToAbsoluteUri(uri).ToString();
            NotifyLocationChanged(false);
        }
    }

    [TestCase("", ViewerPage.ResourceTypes, null, null)]
    [TestCase("resources", ViewerPage.ResourceTypes, null, null)]
    [TestCase("resources/Patient?family=Smith", ViewerPage.ResourceList, "Patient", null)]
    [TestCase("resources/Patient/abc-1.2", ViewerPage.Resource, "Patient", "abc-1.2")]
    [TestCase("summary", ViewerPage.ClinicalSummary, null, null)]
    [TestCase("summary/p1", ViewerPage.ClinicalSummary, "Patient", "p1")]
    public void AnAddressOfTheViewer_IsItsPage(string location, ViewerPage page, string? type, string? id)
    {
        var route = ViewerRoutes.Parse(location);

        Assert.Multiple(() =>
        {
            Assert.That(route?.Page, Is.EqualTo(page));
            Assert.That(route?.ResourceType, Is.EqualTo(type));
            Assert.That(route?.Id, Is.EqualTo(id));
        });
    }

    [TestCase("/resources")]
    [TestCase("resources//Patient")]
    [TestCase("resources/../admin")]
    [TestCase("resources/%2e%2e/admin")]
    [TestCase("resources\\Patient")]
    [TestCase("https://evil.example.test/resources")]
    [TestCase("resources/Pat1ent")]
    [TestCase("resources/Patient/a b")]
    [TestCase("other")]
    public void AnythingElse_IsNotAnAddressOfTheViewer(string location)
    {
        Assert.That(ViewerRoutes.Parse(location), Is.Null);
    }

    [Test]
    public void ARoute_WrittenAndReadAgain_IsTheSame()
    {
        var route = new ViewerRoute(ViewerPage.ResourceList, "Observation", Query: "code=8867-4");

        Assert.That(ViewerRoutes.Parse(route.Location), Is.EqualTo(route));
    }

    [Test]
    public void TheStore_ReadsAndWritesTheAddressBelowItsPrefix_AndTellsWhenItChanges()
    {
        var navigation = new FakeNavigation("https://app.example.test/app/server/42/resources?x=1");
        using var store = new UrlLocationStore(navigation, "/server/42");
        var changes = 0;
        store.LocationChanged += (_, _) => changes++;

        var before = store.Location;
        store.NavigateTo("resources/Patient");

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo("resources?x=1"));
            Assert.That(navigation.Visited, Is.EqualTo(new[] { "server/42/resources/Patient" }));
            Assert.That(store.Location, Is.EqualTo("resources/Patient"));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(() => store.NavigateTo("../admin"), Throws.ArgumentException);
            Assert.That(store.HrefOf("resources/Patient/1"), Is.EqualTo("server/42/resources/Patient/1"), "a link relative to the base of the document");
        });
    }

    [Test]
    public void AnAddressOutsideThePrefix_IsNoneOfTheViewer()
    {
        using var store = new UrlLocationStore(new FakeNavigation("https://app.example.test/app/packages"), "server/42/");

        Assert.That(store.Location, Is.Null);
    }
}
