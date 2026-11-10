using System.Text;
using Flox.Core.Data;
using Flox.Core.Models;
using Flox.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flox.Tests.Platform;

// The bookmarks and the recent pages kept on the device: the file that moves the bookmarks, and the queue of the recent pages
[TestFixture]
public class DeviceUserDataTests
{
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation()
        {
            Initialize("https://app.test/", "https://app.test/");
        }

        public void Visit(string path)
        {
            Uri = "https://app.test/" + path;
        }
    }

    private static LocalGuidEntityStorage<T> StorageOf<T>(ILocalDocumentStore store) where T : class, IGuidEntity
    {
        return new LocalGuidEntityStorage<T>(store, NullLogger<LocalEntityStorage<Guid, T>>.Instance);
    }

    private static UserBookmark ViewerBookmark(string location, string server = "https://fhir.test/r4/")
    {
        return new UserBookmark
        {
            Uid = UserBookmark.KeyOf(UserBookmarkTypes.Viewer, server, location),
            Type = UserBookmarkTypes.Viewer,
            EntityId = location,
            Location = location,
            DisplayName = "John Smith",
            Category = "Patient",
            Url = "server/1/" + location,
            SourceKey = "1",
            SourceName = "Test",
            SourceAddress = UserBookmark.NormalizeAddress(server)
        };
    }

    private static async Task AnExport_ImportsOnAnotherDevice_OnceAndWhole()
    {
        var device = StorageOf<UserBookmark>(new InMemoryDocumentStore());
        await device.InsertAsync(ViewerBookmark("resources/Patient/1"));
        await device.InsertAsync(ViewerBookmark("resources/Patient?family=Smith"));
        var file = await new UserBookmarkTransfer(device).ExportAsync();

        var other = StorageOf<UserBookmark>(new InMemoryDocumentStore());
        await other.InsertAsync(ViewerBookmark("resources/Patient/1", "https://fhir.test/r4"));
        var transfer = new UserBookmarkTransfer(other);
        var first = await transfer.ImportAsync(new MemoryStream(file));
        var again = await transfer.ImportAsync(new MemoryStream(file));

        Assert.That(first, Is.EqualTo(new BookmarkImportResult(1, 1, 0)), "the same place of the same server (without the trailing slash) is the one already there");
        Assert.That(again, Is.EqualTo(new BookmarkImportResult(0, 2, 0)), "a second import adds nothing");
        var search = await other.GetByIdAsync(UserBookmark.KeyOf(UserBookmarkTypes.Viewer, "https://fhir.test/r4", "resources/Patient?family=Smith"));
        Assert.That(search?.Location, Is.EqualTo("resources/Patient?family=Smith"), "a saved search keeps its query");
        Assert.That(search?.SourceAddress, Is.EqualTo("https://fhir.test/r4"));
    }

    private static async Task AFileThatIsNotAnExport_IsRefused()
    {
        var transfer = new UserBookmarkTransfer(StorageOf<UserBookmark>(new InMemoryDocumentStore()));
        foreach (var text in new[] { "not json", "{\"format\":\"other\",\"version\":1,\"bookmarks\":[]}", "{\"format\":\"flox-bookmarks\",\"version\":99,\"bookmarks\":[]}" })
        {
            await Assert.ThatAsync(() => transfer.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(text))), Throws.TypeOf<InvalidDataException>(), text);
        }

        var skipped = await transfer.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes("{\"format\":\"flox-bookmarks\",\"version\":1,\"bookmarks\":[{\"type\":\"fhir-viewer\"}]}")));
        Assert.That(skipped, Is.EqualTo(new BookmarkImportResult(0, 0, 1)), "an entry without an identity is skipped, not the file");
    }

    private static async Task RecentPages_AreAQueue_OfTheNewest()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILocalDocumentStore, InMemoryDocumentStore>();
        services.AddLocalEntity<UserRecent>();
        var navigation = new TestNavigation();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var recent = new RecentVisitService(navigation, scope.ServiceProvider, NullLogger<RecentVisitService>.Instance);

        for (var i = 0; i < RecentVisitService.Capacity + 5; i++)
        {
            navigation.Visit($"page/{i}");
            await recent.VisitAsync($"Page {i}", null, null);
        }

        navigation.Visit("page/0?filter=again");
        await recent.VisitAsync("Page 0", null, null);

        var storage = scope.ServiceProvider.GetRequiredService<IGuidEntityStorage<UserRecent>>();
        var all = await recent.GetRecentAsync(1000);
        Assert.That(await storage.CountAsync(), Is.EqualTo(RecentVisitService.Capacity), "never more than the capacity");
        Assert.That(all[0].EntityId, Is.EqualTo("/page/0"), "a visited page is one row, moved to the head (the query is not part of it)");
        Assert.That(all.Select(row => row.EntityId), Has.None.EqualTo("/page/5"), "the oldest went first");
        Assert.That(all.Select(row => row.EntityId), Has.Some.EqualTo("/page/6"));
    }

    [Test]
    public async Task DeviceUserDataScenario()
    {
        await Assert.MultipleAsync(async () =>
        {
            await AnExport_ImportsOnAnotherDevice_OnceAndWhole();
            await AFileThatIsNotAnExport_IsRefused();
            await RecentPages_AreAQueue_OfTheNewest();
        });
    }
}
