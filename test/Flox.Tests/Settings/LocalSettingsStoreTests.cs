using Flox.Core.Data;
using Flox.Core.Settings;

namespace Flox.Tests.Settings;

// The settings of a client without an application server, on the device
public class LocalSettingsStoreTests
{
    [Test]
    public async Task TheKeysOfAModule_GoToItsStore_TheOthersToTheStoreOfTheApplication()
    {
        var core = new InMemoryDocumentStore();
        var module = new InMemoryDocumentStore();
        using var store = new LocalSettingsStore(core, id => id == "fhir" ? module : null, [module]);

        await store.SetAsync("fhir", "{\"release\":\"R5\"}");
        await store.SetAsync("theme", "{\"dark\":true}");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await module.LoadAsync(LocalSettingsStore.Collection)).Select(d => d.Key), Is.EqualTo(new[] { "fhir" }));
            Assert.That((await core.LoadAsync(LocalSettingsStore.Collection)).Select(d => d.Key), Is.EqualTo(new[] { "theme" }));
            Assert.That(await store.GetAsync("fhir"), Is.EqualTo("{\"release\":\"R5\"}"));
            Assert.That(await store.GetAsync("theme"), Is.EqualTo("{\"dark\":true}"));
        });
    }

    [Test]
    public async Task ASetting_IsKeptAndRemoved_AndEachChangeIsTold()
    {
        using var store = new LocalSettingsStore(new InMemoryDocumentStore());
        var told = new List<string>();
        store.Changed += (_, e) => told.Add(e.Key);

        await store.SetAsync("theme", "{\"dark\":true}");
        var kept = await store.GetAsync("theme");
        await store.SetAsync("theme", null);
        var removed = await store.GetAsync("theme");

        Assert.Multiple(() =>
        {
            Assert.That(kept, Is.EqualTo("{\"dark\":true}"));
            Assert.That(removed, Is.Null);
            Assert.That(told, Is.EqualTo(new[] { "theme", "theme" }));
        });
    }

    [Test]
    public async Task AChangeInAnotherTab_TellsEveryKeyOfTheSettings()
    {
        var documents = new TabDocumentStore();
        using var store = new LocalSettingsStore(documents);
        await store.SetAsync("theme", "1");
        await store.SetAsync("general", "2");
        var told = new List<string>();
        store.Changed += (_, e) => told.Add(e.Key);

        documents.ChangeElsewhere("bookmarks");
        documents.ChangeElsewhere(LocalSettingsStore.Collection);
        await Task.Yield();

        Assert.That(told, Is.EquivalentTo(new[] { "theme", "general" }));
    }

    // The documents of the tab, and a write of another tab on demand
    private sealed class TabDocumentStore : ILocalDocumentStore
    {
        private readonly InMemoryDocumentStore _inner = new();

        public event EventHandler<LocalDocumentsChangedEventArgs>? ChangedElsewhere;

        public void ChangeElsewhere(string collection)
        {
            ChangedElsewhere?.Invoke(this, new LocalDocumentsChangedEventArgs(collection));
        }

        public ValueTask<IReadOnlyList<LocalDocument>> LoadAsync(string collection, CancellationToken cancellationToken = default)
        {
            return _inner.LoadAsync(collection, cancellationToken);
        }

        public ValueTask PutAsync(string collection, IReadOnlyCollection<LocalDocument> documents, CancellationToken cancellationToken = default)
        {
            return _inner.PutAsync(collection, documents, cancellationToken);
        }

        public ValueTask DeleteAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
        {
            return _inner.DeleteAsync(collection, keys, cancellationToken);
        }
    }
}
