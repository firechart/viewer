using Flox.Core.Data;

namespace Flox.Tests.Platform;

// The values a component keeps on the device: they outlive the scope, a missing or broken one is the default, and a device without a store breaks nothing
[TestFixture]
public class KeyValueStorageTests
{
    [Test]
    public async Task AValue_IsReadByTheNextScope_AndAMissingOneIsTheDefault()
    {
        var device = new InMemoryDocumentStore();
        using (var first = new LocalKeyValueStorage(device))
        {
            await first.SetValueAsync("codemirror.word-wrap", false);
        }

        using var next = new LocalKeyValueStorage(device);
        var wrap = await next.GetValueAsync("codemirror.word-wrap", true);
        var width = await next.GetValueAsync("pane.width", 320);
        Assert.Multiple(() =>
        {
            Assert.That(wrap, Is.False);
            Assert.That(width, Is.EqualTo(320));
        });
    }

    [Test]
    public async Task AValueThatDoesNotReadAsItsType_IsTheDefault()
    {
        var device = new InMemoryDocumentStore();
        await device.PutAsync(LocalKeyValueStorage.Collection, [new LocalDocument("pane.width", "\"wide\"")]);
        using var storage = new LocalKeyValueStorage(device);

        Assert.That(await storage.GetValueAsync("pane.width", 320), Is.EqualTo(320));
    }

    [Test]
    public async Task WithoutAStore_TheDefaultIsRead_AndNothingFails()
    {
        using var storage = new LocalKeyValueStorage(new GoneStore());

        await storage.SetValueAsync("codemirror.word-wrap", false);
        Assert.That(await storage.GetValueAsync("codemirror.word-wrap", true), Is.True);
    }

    // The store of a page that is closing, or of prerendering
    private sealed class GoneStore : ILocalDocumentStore
    {
        public event EventHandler<LocalDocumentsChangedEventArgs>? ChangedElsewhere
        {
            add { }
            remove { }
        }

        public ValueTask<IReadOnlyList<LocalDocument>> LoadAsync(string collection, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("No store");
        }

        public ValueTask PutAsync(string collection, IReadOnlyCollection<LocalDocument> documents, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("No store");
        }

        public ValueTask DeleteAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("No store");
        }
    }
}
