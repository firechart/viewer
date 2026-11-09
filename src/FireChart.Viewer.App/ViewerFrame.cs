using System.Text.Json;
using System.Text.Json.Nodes;
using FireChart.Viewer.Appearance;
using Microsoft.JSInterop;

namespace FireChart.Viewer.App;

// The messages between the frame (/embed) and the page that embeds it (the SDK, the sandbox), over postMessage; viewer-frame.js lets through only the
// origins of host.json. Parent to frame: init, update, restart, navigate, each with an optional id. Frame to parent: hello, ready, restarting, updated,
// navigate, resize, error, and result { ok, value | error } for a request with an id
internal sealed class ViewerFrameChannel : IAsyncDisposable
{
    private readonly IJSObjectReference _module;
    private readonly DotNetObjectReference<ViewerFrameChannel> _self;
    private readonly Func<FrameRequest, Task> _onMessage;

    private ViewerFrameChannel(IJSObjectReference module, Func<FrameRequest, Task> onMessage)
    {
        _module = module;
        _onMessage = onMessage;
        _self = DotNetObjectReference.Create(this);
    }

    // None when the frame is not hosted by a parent that sends the configuration: it reads viewer.json
    public static async Task<ViewerFrameChannel?> OpenAsync(IJSRuntime js, Func<FrameRequest, Task> onMessage)
    {
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/viewer-frame.js");
        if (!await module.InvokeAsync<bool>("isParentHosted"))
        {
            await module.DisposeAsync();
            return null;
        }

        var channel = new ViewerFrameChannel(module, onMessage);
        await module.InvokeVoidAsync("connect", channel._self);
        return channel;
    }

    [JSInvokable]
    public Task OnMessage(string type, string? id, string payload)
    {
        return _onMessage(new FrameRequest(type, id, JsonNode.Parse(payload) as JsonObject ?? []));
    }

    public ValueTask PostAsync(string type, JsonObject? payload = null)
    {
        return _module.InvokeVoidAsync("post", type, null, (payload ?? []).ToJsonString());
    }

    // The answer to a request with an id; a request without one (the sandbox) gets nothing
    public ValueTask SucceedAsync(FrameRequest request, JsonObject? value = null)
    {
        return request.Id is null
            ? ValueTask.CompletedTask
            : _module.InvokeVoidAsync("post", "result", request.Id, new JsonObject { ["ok"] = true, ["value"] = value ?? [] }.ToJsonString());
    }

    // A failed request: its promise in the SDK is rejected with the first error; without an id the errors are events
    public async ValueTask FailAsync(FrameRequest request, string type, IReadOnlyList<ViewerConfigError> errors)
    {
        if (request.Id is null)
        {
            await PostErrorsAsync(type, errors);
            return;
        }

        var result = new JsonObject
        {
            ["ok"] = false,
            ["error"] = ToJson(type, errors[0]),
            ["errors"] = new JsonArray([.. errors.Select(e => (JsonNode)ToJson(type, e))])
        };
        await _module.InvokeVoidAsync("post", "result", request.Id, result.ToJsonString());
    }

    public async ValueTask PostErrorsAsync(string type, IEnumerable<ViewerConfigError> errors)
    {
        foreach (var error in errors)
        {
            await PostAsync("error", ToJson(type, error));
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _module.InvokeVoidAsync("disconnect");
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // the page is gone
        }

        _self.Dispose();
    }

    private static JsonObject ToJson(string type, ViewerConfigError error)
    {
        return new JsonObject { ["type"] = type, ["code"] = error.Code, ["param"] = error.Param, ["message"] = error.Message };
    }
}

internal sealed record FrameRequest(string Type, string? Id, JsonObject Payload);

// update: the options that change without a restart (plan 4.8, live and reload) merged into the current configuration by key; server and smart need a restart
internal static class ViewerConfigPatch
{
    private static readonly HashSet<string> s_updatable = new(["locale", "appearance", "terminology", "clinicalSummary"], StringComparer.Ordinal);

    public static ViewerConfigPatchResult Apply(string current, JsonObject patch)
    {
        var restart = patch.Select(p => p.Key).FirstOrDefault(k => !s_updatable.Contains(k));
        if (restart is not null)
        {
            return new ViewerConfigPatchResult(null, [], new ViewerConfigError("restart_required", restart, $"'{restart}' changes with a restart, not with an update."));
        }

        var merged = JsonNode.Parse(current, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
                     as JsonObject ?? [];
        Merge(merged, patch);
        return new ViewerConfigPatchResult(merged.ToJsonString(), patch.Select(p => p.Key).ToList(), null);
    }

    // Objects merge by key, everything else is replaced
    public static void Merge(JsonObject target, JsonObject patch)
    {
        foreach (var (key, value) in patch.ToList())
        {
            if (value is JsonObject inner && target[key] is JsonObject existing)
            {
                Merge(existing, inner);
            }
            else
            {
                target[key] = value?.DeepClone();
            }
        }
    }
}

internal sealed record ViewerConfigPatchResult(string? Text, IReadOnlyList<string> Applied, ViewerConfigError? Error);
