using Flox.Services;
using Microsoft.JSInterop;

namespace Flox.Tests.Platform;

// The import of a module that came back null is tried again, so a library that keeps the first answer of an import (Blazor Blueprint) never keeps
// a null; when every attempt is empty the import fails, and a failure is not kept
public class ImportRetryingJSRuntimeTests
{
    private sealed class FakeModule : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeRuntime(params IJSObjectReference?[] imports) : IJSInProcessRuntime
    {
        private readonly Queue<IJSObjectReference?> _imports = new(imports);

        public List<string> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            object? result = identifier == "import" ? _imports.Dequeue() : "value";
            return ValueTask.FromResult((TValue)result!);
        }

        public TResult Invoke<TResult>(string identifier, params object?[]? args)
        {
            throw new NotSupportedException();
        }
    }

    [Test]
    public async Task An_import_that_came_back_null_is_tried_again()
    {
        var module = new FakeModule();
        var inner = new FakeRuntime(null, null, module);
        IJSRuntime runtime = new ImportRetryingJSRuntime(inner);

        var imported = await runtime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorBlueprint.Components/core.js");

        Assert.That(imported, Is.SameAs(module));
        Assert.That(inner.Calls, Has.Count.EqualTo(3));
    }

    [Test]
    public void An_import_empty_every_time_fails()
    {
        IJSRuntime runtime = new ImportRetryingJSRuntime(new FakeRuntime(null, null, null, null));

        Assert.That(async () => await runtime.InvokeAsync<IJSObjectReference>("import", "./x.js"), Throws.InvalidOperationException);
    }

    [Test]
    public async Task Any_other_call_goes_through_once()
    {
        var inner = new FakeRuntime();
        IJSRuntime runtime = new ImportRetryingJSRuntime(inner);

        var value = await runtime.InvokeAsync<string>("sidebar.initialize", 1);

        Assert.That(value, Is.EqualTo("value"));
        Assert.That(inner.Calls, Is.EqualTo(new[] { "sidebar.initialize" }));
    }
}
