using System.Diagnostics.CodeAnalysis;
using Flox.Sqlite;
using Microsoft.JSInterop;

namespace Flox.Tests.Platform;

// The interop of the page now and then answers the open of a base with nothing although the script returned its handle (the home page of Pro, its
// bookmarks and recent pages at once, 2026-11-21): the open is tried again instead of failing the page
public class BrowserSqliteEngineTests
{
    [Test]
    public async Task AnOpenAnsweredWithNothing_IsTriedAgain()
    {
        var module = new Module(emptyOpens: 2);
        var engine = new BrowserSqliteEngine(new Runtime(module));

        await using var database = await engine.OpenAsync("flox.sqlite", SqliteAccess.ReadWrite);

        Assert.That(module.Opens, Is.EqualTo(3));
    }

    [Test]
    public async Task AnOpenThatNeverAnswers_FailsWithItsReason()
    {
        var engine = new BrowserSqliteEngine(new Runtime(new Module(emptyOpens: int.MaxValue)));

        var error = await Assert.ThrowsAsync<SqliteEngineException>(() => engine.OpenAsync("flox.sqlite", SqliteAccess.ReadWrite));

        Assert.That(error!.Message, Does.Contain("without a handle"));
    }

    private sealed class Runtime(IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, object?[]? args)
        {
            return ValueTask.FromResult((TValue)module);
        }

        public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }
    }

    private sealed class Module(int emptyOpens) : IJSObjectReference
    {
        public int Opens { get; private set; }

        public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, object?[]? args)
        {
            object? result = identifier switch
            {
                "init" => "3.50",
                "open" when ++Opens <= emptyOpens => null,
                "open" => (int?)7,
                _ => default(TValue)
            };
            return ValueTask.FromResult((TValue)result!);
        }

        public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
