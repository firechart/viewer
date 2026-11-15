using FireChart.Viewer.Core.Fhir;
using FireChart.Viewer.Core.Forms;
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
    [TestCase("summary", ViewerPage.ResourceList, "Patient", null)]
    [TestCase("summary/Patient/p1", ViewerPage.Summary, "Patient", "p1")]
    [TestCase("summary/p1", ViewerPage.Summary, "Patient", "p1")]
    [TestCase("summary/Encounter/e1", ViewerPage.Summary, "Encounter", "e1")]
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

    [Test]
    public void TheOlderAddressOfAPatientSummary_IsWrittenInTheNewForm()
    {
        Assert.That(ViewerRoutes.Parse("summary/p1")?.Location, Is.EqualTo("summary/Patient/p1"), "bookmarks and links of before 2026-11-11 still open");
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

    private sealed record Server(string Key, string Name, string BaseUrl) : IViewerServer
    {
        public AuthenticationType AuthenticationType => AuthenticationType.None;

        public Hl7.Fhir.Specification.FhirRelease? FhirRelease => null;

        public int PageSize => 20;
    }

    [Test]
    public void ARecordInASheet_IsInTheAddressOfTheSummary_AndClosingTheOneTheViewerOpened_GoesBack()
    {
        var navigation = new FakeNavigation("https://app.example.test/app/summary/p1");
        using var store = new UrlLocationStore(navigation, null);
        var session = new ViewerSession(new Server("s", "S", "https://s.test/fhir/"), store);
        using var linkedStore = new UrlLocationStore(new FakeNavigation("https://app.example.test/app/summary/p1?x=1&peek=Condition/c1"), null);
        var linked = new ViewerSession(new Server("s", "S", "https://s.test/fhir/"), linkedStore);

        session.OpenInSheet(new ViewerRoute(ViewerPage.Resource, "Observation", "o1"));
        var opened = ViewerRoutes.Parse(store.Location)?.Peek;
        var goesBack = session.CloseSheet();
        var linkedGoesBack = linked.CloseSheet();

        Assert.Multiple(() =>
        {
            Assert.That(store.Location, Is.EqualTo("summary/Patient/p1?peek=Observation/o1"));
            Assert.That(opened, Is.EqualTo(new ViewerRoute(ViewerPage.Resource, "Observation", "o1")));
            Assert.That(goesBack, Is.True, "Back closes it, so the history has no second entry of the summary");
            Assert.That(linkedGoesBack, Is.False, "a sheet that came with a link is closed by replacing the address");
            Assert.That(linkedStore.Location, Is.EqualTo("summary/Patient/p1?x=1"));
            Assert.That(ViewerRoutes.Parse("summary/p1?peek=Observation/..%2Fx")?.Peek, Is.Null, "not a record");
            Assert.That(ViewerRoutes.Parse("resources/Patient?peek=Observation/o1")?.Peek, Is.Null, "only the summary has a sheet");
        });
    }

    [Test]
    public void ARecordOpensInASheet_ExceptAPatientAndAnEncounter_UnlessTheSectionSays()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RecordOpening.Resolve(null, "Observation"), Is.EqualTo(RecordOpening.Sheet));
            Assert.That(RecordOpening.Resolve(null, "Encounter"), Is.EqualTo(RecordOpening.Page), "the records of others are listed on its page");
            Assert.That(RecordOpening.Resolve("page", "Observation"), Is.EqualTo(RecordOpening.Page));
            Assert.That(RecordOpening.Resolve("dialog", "Observation"), Is.EqualTo(RecordOpening.Sheet), "an unknown value is the default");
        });
    }

    [Test]
    public void AnAddressOutsideThePrefix_IsNoneOfTheViewer()
    {
        using var store = new UrlLocationStore(new FakeNavigation("https://app.example.test/app/packages"), "server/42/");

        Assert.That(store.Location, Is.Null);
    }
}
