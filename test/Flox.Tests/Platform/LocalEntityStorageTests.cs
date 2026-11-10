using Flox.Core;
using Flox.Core.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flox.Tests.Platform;

[TestFixture]
public class LocalEntityStorageTests
{
    private const string Collection = "Note";

    public class Note : GuidEntity
    {
        [Index(Like = true)]
        public string Title { get; set; } = string.Empty;

        [Index(FullText = FullTextWeight.B)]
        public string? Text { get; set; }

        public int Size { get; set; }

        public List<string> Tags { get; } = [];
    }

    public class Plain : GuidEntity
    {
        public string Title { get; set; } = string.Empty;
    }

    private static LocalGuidEntityStorage<T> StorageOf<T>(ILocalDocumentStore store) where T : class, IGuidEntity
    {
        return new LocalGuidEntityStorage<T>(store, NullLogger<LocalEntityStorage<Guid, T>>.Instance, collection: typeof(T).Name);
    }

    private static async Task<List<Note>> SeedAsync(LocalGuidEntityStorage<Note> storage, params string[] titles)
    {
        var rows = new List<Note>();
        for (var i = 0; i < titles.Length; i++)
        {
            rows.Add(await storage.InsertAsync(new Note { Title = titles[i], Size = i }));
        }

        return rows;
    }

    private static async Task Rows_OutliveTheStorage_AndCallersGetCopies()
    {
        var store = new InMemoryDocumentStore();
        using var first = StorageOf<Note>(store);
        var note = new Note { Title = "Alpha", Text = "first" };
        note.Tags.Add("a");
        await first.InsertAsync(note);

        var copy = (await first.GetByIdAsync(note.Uid))!;
        copy.Title = "changed, not stored";

        using var second = StorageOf<Note>(store);
        var reloaded = await second.GetByIdAsync(note.Uid);

        Assert.That(reloaded, Is.Not.Null, "a new storage reads what the first one wrote");
        Assert.That(reloaded!.Title, Is.EqualTo("Alpha"), "a change of a returned row is not stored without UpdateAsync");
        Assert.That(reloaded.Tags, Is.EqualTo(new[] { "a" }), "a collection without a setter survives the store");
        Assert.That(reloaded.Revision, Is.EqualTo(1));
        Assert.That((await first.GetByIdAsync(note.Uid))!.Title, Is.EqualTo("Alpha"), "the cache was not changed through the copy");
    }

    private static async Task Insert_OfAnExistingKey_IsADuplicate()
    {
        using var storage = StorageOf<Note>(new InMemoryDocumentStore());
        var note = await storage.InsertAsync(new Note { Title = "One" });

        await Assert.ThatAsync(() => storage.InsertAsync(new Note { Uid = note.Uid, Title = "Two" }), Throws.TypeOf<DuplicateException>());
        Assert.That((await storage.GetByIdAsync(note.Uid))!.Title, Is.EqualTo("One"));
    }

    private static async Task Update_TakesTheStoredRevision_AndKeepsTheImmutableFields()
    {
        var store = new InMemoryDocumentStore();
        using var storage = StorageOf<Note>(store);
        var note = await storage.InsertAsync(new Note { Title = "One" });
        var created = note.CreatedAt;

        var read = (await storage.GetByIdAsync(note.Uid))!;
        var stale = (await storage.GetByIdAsync(note.Uid))!;
        read.Title = "Two";
        read.CreatedAt = DateTimeOffset.MinValue;
        await storage.UpdateAsync(read.Uid, read);

        stale.Title = "Three";
        await Assert.ThatAsync(() => storage.UpdateAsync(stale.Uid, stale), Throws.TypeOf<ConcurrentUpdateException>(), "a copy read before the change");
        await Assert.ThatAsync(() => storage.UpdateAsync(Guid.NewGuid(), new Note()), Throws.TypeOf<ArgumentException>(), "the key differs from the key of the entity");
        var missing = new Note();
        await Assert.ThatAsync(() => storage.UpdateAsync(missing.Uid, missing), Throws.TypeOf<NotFoundException>());

        using var reloaded = StorageOf<Note>(store);
        var stored = (await reloaded.GetByIdAsync(note.Uid))!;
        Assert.That(stored.Title, Is.EqualTo("Two"));
        Assert.That(stored.Revision, Is.EqualTo(2));
        Assert.That(stored.CreatedAt, Is.EqualTo(created), "CreatedAt is immutable");
        Assert.That(stored.UpdatedAt, Is.Not.Null);
    }

    private static async Task Find_Filters_Sorts_AndPages()
    {
        using var storage = StorageOf<Note>(new InMemoryDocumentStore());
        await SeedAsync(storage, "a", "b", "c", "d", "e", "f");

        var request = new PagedRequest<Note> { Skip = 1, PageSize = 2 };
        request.Filters.Add(row => row.Size >= 1);
        request.Filters.SetValue(nameof(Note.Size), 5, FilterOperator.Lt);
        request.Sorts.SetValue(nameof(Note.Size), ascending: false);
        var page = await storage.FindAsync(request);

        Assert.That(page.Total, Is.EqualTo(4), "sizes 1..4, before the paging");
        Assert.That(page.Items.Select(row => row.Title), Is.EqualTo(new[] { "d", "c" }), "descending, the first row skipped, two taken");

        var all = await storage.FindAsync(new PagedRequest<Note> { PageSize = 0, ReturnTotal = false });
        Assert.That(all.Items, Has.Count.EqualTo(6), "a page size of zero is no limit");
        Assert.That(all.Total, Is.Null, "no total when it was not asked");
        Assert.That(await storage.CountAsync(row => row.Size > 3), Is.EqualTo(2));
    }

    private static async Task Search_FindsEveryWord_AndRanksTheExactTitleFirst()
    {
        using var storage = StorageOf<Note>(new InMemoryDocumentStore());
        await storage.InsertAsync(new Note { Title = "Observation of the patient", Text = "vital signs" });
        await storage.InsertAsync(new Note { Title = "Observation" });
        await storage.InsertAsync(new Note { Title = "Observation profile" });
        await storage.InsertAsync(new Note { Title = "Lab observation" });
        await storage.InsertAsync(new Note { Title = "Unrelated", Text = "an observation in the text" });
        await storage.InsertAsync(new Note { Title = "Nothing" });

        var found = await storage.FindAsync(new PagedRequest<Note> { FullTextSearchString = "OBSERVATION" });
        Assert.That(found.Items.Select(row => row.Title), Is.EqualTo(new[] { "Observation", "Observation profile", "Observation of the patient", "Lab observation", "Unrelated" }),
                    "equal, then starts with (shorter first), then contains, then found only in the text");
        Assert.That(found.Total, Is.EqualTo(5));

        var words = await storage.FindAsync(new PagedRequest<Note> { FullTextSearchString = "patient vital" });
        Assert.That(words.Items.Select(row => row.Title), Is.EqualTo(new[] { "Observation of the patient" }), "every word, in any of the searched properties");

        var sorted = new PagedRequest<Note> { FullTextSearchString = "observation" };
        sorted.Sorts.SetValue(nameof(Note.Title));
        Assert.That((await storage.FindAsync(sorted)).Items[0].Title, Is.EqualTo("Lab observation"), "sorts of the request win over the relevance");

        using var plain = StorageOf<Plain>(new InMemoryDocumentStore());
        await plain.InsertAsync(new Plain { Title = "x" });
        Assert.That((await plain.FindAsync(new PagedRequest<Plain> { FullTextSearchString = "nothing like it" })).Items, Has.Count.EqualTo(1),
                    "an entity with no searched property ignores the text, as the other storages");
    }

    private static async Task DeleteMany_OverTheLimit_DeletesNothing()
    {
        var store = new InMemoryDocumentStore();
        using var storage = StorageOf<Note>(store);
        var rows = await SeedAsync(storage, "a", "b", "c");

        await Assert.ThatAsync(() => storage.DeleteManyAsync(row => row.Size >= 0, limit: 2), Throws.TypeOf<BatchLimitExceededException>());
        Assert.That(await storage.CountAsync(), Is.EqualTo(3));

        Assert.That(await storage.DeleteManyAsync(row => row.Size >= 1), Is.EqualTo(2));
        await storage.DeleteAsync(rows[0].Uid);
        await Assert.ThatAsync(() => storage.DeleteAsync(rows[0].Uid), Throws.TypeOf<NotFoundException>());
        Assert.That(await store.LoadAsync(Collection), Is.Empty, "the deletes reached the store");
    }

    private static async Task ABrokenDocument_IsLeftOut()
    {
        var store = new InMemoryDocumentStore();
        await store.PutAsync(Collection, [new LocalDocument("broken", "{ not json")]);
        using var storage = StorageOf<Note>(store);
        await storage.InsertAsync(new Note { Title = "fine" });

        Assert.That(await storage.CountAsync(), Is.EqualTo(1));
        Assert.That(await store.LoadAsync(Collection), Has.Count.EqualTo(2), "the broken document stays in the store");
    }

    private static async Task Capabilities_AreAllOrNothing()
    {
        using var storage = StorageOf<Note>(new InMemoryDocumentStore());
        var note = await storage.InsertAsync(new Note { Title = "a" });
        var missing = Guid.NewGuid();

        var capabilities = (await storage.GetCapabilitiesAsync([note.Uid, missing])).ToDictionary(row => row.Key);
        Assert.That(capabilities[note.Uid], Is.EqualTo(new EntityCapability<Guid>(note.Uid, true, true, true)));
        Assert.That(capabilities[missing], Is.EqualTo(new EntityCapability<Guid>(missing, false, false, false)));
    }

    [Test]
    public async Task LocalEntityStorageScenario()
    {
        await Assert.MultipleAsync(async () =>
        {
            await Rows_OutliveTheStorage_AndCallersGetCopies();
            await Insert_OfAnExistingKey_IsADuplicate();
            await Update_TakesTheStoredRevision_AndKeepsTheImmutableFields();
            await Find_Filters_Sorts_AndPages();
            await Search_FindsEveryWord_AndRanksTheExactTitleFirst();
            await DeleteMany_OverTheLimit_DeletesNothing();
            await ABrokenDocument_IsLeftOut();
            await Capabilities_AreAllOrNothing();
        });
    }
}
