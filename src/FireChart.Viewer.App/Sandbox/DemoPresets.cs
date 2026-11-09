using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;

namespace FireChart.Viewer.App.Sandbox;

// A public server of the sandbox (demo-presets.json of the application, not of the package): its configuration and a patient with much data
internal sealed record DemoPreset(string Id, string Name, string Description, string? Patient, JsonObject Config)
{
    public string Url => Config["server"]?["url"]?.GetValue<string>() ?? string.Empty;
}

internal enum PresetAvailability
{
    Checking,
    Available,
    Unavailable
}

internal sealed class DemoPresets(IHttpClientFactory httpClientFactory, NavigationManager navigationManager)
{
    public const string FileName = "demo-presets.json";

    private static readonly TimeSpan s_checkTimeout = TimeSpan.FromSeconds(15);

    public async Task<IReadOnlyList<DemoPreset>> LoadAsync(CancellationToken cancellationToken = default)
    {
        using var http = httpClientFactory.CreateClient();
        var text = await http.GetStringAsync(navigationManager.ToAbsoluteUri(FileName), cancellationToken);
        var presets = new List<DemoPreset>();
        foreach (var node in JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }) as JsonArray ?? [])
        {
            if (node is JsonObject { } item && item["config"] is JsonObject config)
            {
                presets.Add(new DemoPreset(Text(item, "id"), Text(item, "name"), Text(item, "description"), item["patient"]?.GetValue<string>(), config));
            }
        }

        return presets;
    }

    // The CapabilityStatement answers: the server is up and lets this origin read it (CORS)
    public async Task<PresetAvailability> CheckAsync(DemoPreset preset, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(s_checkTimeout);
        try
        {
            using var http = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, preset.Url.TrimEnd('/') + "/metadata?_summary=true");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return response.IsSuccessStatusCode ? PresetAvailability.Available : PresetAvailability.Unavailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or UriFormatException or InvalidOperationException)
        {
            return PresetAvailability.Unavailable;
        }
    }

    private static string Text(JsonObject item, string name)
    {
        return item[name]?.GetValue<string>() ?? string.Empty;
    }
}
