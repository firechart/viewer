using FireChart.Viewer.Core.Fhir;
using Flox.Core.Data;

namespace FireChart.Viewer.Tests.Viewer;

// The last search of a list is kept per server and type in the documents of the device (IndexedDB); a cleared search (empty) is not the same as none;
// a search kept by an older version in localStorage is moved over
public class ListSearchMemoryTests
{
    private sealed class Storage : ILocalStorage
    {
        public Dictionary<string, string> Items { get; } = new(StringComparer.Ordinal);

        public string StorageName => "test";

        public Task<List<string>> GetAllKeysAsync()
        {
            return Task.FromResult(Items.Keys.ToList());
        }

        public Task<string?> GetAsync(string key)
        {
            return Task.FromResult(Items.TryGetValue(key, out var value) ? value : null);
        }

        public Task RemoveAsync(string key)
        {
            Items.Remove(key);
            return Task.CompletedTask;
        }

        public Task SetAsync(string key, string? value)
        {
            if (value is null)
            {
                Items.Remove(key);
            }
            else
            {
                Items[key] = value;
            }

            return Task.CompletedTask;
        }
    }

    private sealed record Server(string Key, string Name, string BaseUrl) : IViewerServer
    {
        public AuthenticationType AuthenticationType => AuthenticationType.None;

        public Hl7.Fhir.Specification.FhirRelease? FhirRelease => null;

        public int PageSize => 20;
    }

    [Test]
    public async Task ASearch_IsKeptPerServerAndType_AndACleared_OneIsNotNone()
    {
        var store = new InMemoryDocumentStore();
        var memory = new LocalListSearchMemory(store);
        var a = new Server("a", "A", "https://a.test/fhir/");
        var b = new Server("b", "B", "https://b.test/fhir");

        await memory.SetAsync(a, "StructureDefinition", "kind=resource");
        await memory.SetAsync(a, "ValueSet", string.Empty);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await memory.GetAsync(a, "StructureDefinition"), Is.EqualTo("kind=resource"));
            Assert.That(await memory.GetAsync(a, "ValueSet"), Is.Empty, "cleared by the user");
            Assert.That(await memory.GetAsync(a, "CodeSystem"), Is.Null, "never searched");
            Assert.That(await memory.GetAsync(b, "StructureDefinition"), Is.Null, "another server");
            Assert.That(await new LocalListSearchMemory(store).GetAsync(a, "StructureDefinition"), Is.EqualTo("kind=resource"), "kept in the store, not in the instance");
        });
    }

    [Test]
    public async Task ASearchOfLocalStorage_IsMovedToTheStore_OnTheFirstRead()
    {
        var store = new InMemoryDocumentStore();
        var legacy = new Storage();
        var server = new Server("a", "A", "https://a.test/fhir/");
        legacy.Items["fx-viewer.search|a|https://a.test/fhir|Patient"] = "family=Smith";

        var moved = await new LocalListSearchMemory(store, legacy).GetAsync(server, "Patient");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(moved, Is.EqualTo("family=Smith"));
            Assert.That(legacy.Items, Is.Empty, "removed from localStorage");
            Assert.That(await new LocalListSearchMemory(store).GetAsync(server, "Patient"), Is.EqualTo("family=Smith"), "now in the store");
        });
    }
}
