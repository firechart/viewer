using System.Net.Http.Headers;
using System.Text.Json;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// The requests to FHIR servers, through a named <see cref="HttpClient"/> of their own: never the client of the host application, whose token must not
/// go to a FHIR server.
/// </summary>
/// <example>
/// <code>
/// using var metadata = await FhirServerHttp.GetJsonAsync(httpClientFactory, $"{server.BaseUrl}/metadata", credentials.GetAuthorization(server), cancellationToken);
/// </code>
/// </example>
public static class FhirServerHttp
{
    /// <summary>
    /// The name of the <see cref="HttpClient"/> of the FHIR servers (<see cref="FhirViewerCoreModule"/>).
    /// </summary>
    public const string HttpClientName = "fhir-server";

    /// <summary>
    /// How long a request to a FHIR server may take by default, in seconds (<see cref="IViewerServer.TimeoutSeconds"/>).
    /// </summary>
    public const int DefaultTimeoutSeconds = 60;

    /// <summary>
    /// The shortest time a server may be given, in seconds.
    /// </summary>
    public const int MinTimeoutSeconds = 5;

    /// <summary>
    /// The longest time a server may be given, in seconds.
    /// </summary>
    public const int MaxTimeoutSeconds = 600;

    /// <summary>
    /// The time a request to the server may take: its <see cref="IViewerServer.TimeoutSeconds"/> within <see cref="MinTimeoutSeconds"/> and
    /// <see cref="MaxTimeoutSeconds"/>.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <returns>The time.</returns>
    /// <example>
    /// <code>
    /// using var document = await FhirServerHttp.GetJsonAsync(factory, url, null, FhirServerHttp.TimeoutOf(server), cancellationToken);
    /// </code>
    /// </example>
    public static TimeSpan TimeoutOf(IViewerServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        return TimeSpan.FromSeconds(Math.Clamp(server.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds));
    }

    /// <summary>
    /// Gets a JSON document (<c>Accept: application/fhir+json</c>).
    /// </summary>
    /// <param name="httpClientFactory">Makes the client <see cref="HttpClientName"/>.</param>
    /// <param name="url">The absolute address.</param>
    /// <param name="authorization">The value of the <c>Authorization</c> header (<c>Bearer ...</c>), or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The document; the caller disposes it.</returns>
    /// <exception cref="HttpRequestException">The server did not answer, or answered with an error status (<see cref="HttpRequestException.StatusCode"/>).</exception>
    /// <exception cref="System.Text.Json.JsonException">The answer is not JSON.</exception>
    public static Task<JsonDocument> GetJsonAsync(IHttpClientFactory httpClientFactory, string url, string? authorization, CancellationToken cancellationToken)
    {
        return GetJsonAsync(httpClientFactory, url, authorization, TimeSpan.FromSeconds(DefaultTimeoutSeconds), cancellationToken);
    }

    /// <summary>
    /// What a server said about an error: the <c>diagnostics</c> of the first issue of severity <c>fatal</c> or <c>error</c> of an <c>OperationOutcome</c>
    /// (else of the first issue), else its <c>details.text</c>, else the code of the issue. At most 300 characters, on one line.
    /// </summary>
    /// <param name="body">The body of the answer.</param>
    /// <returns>The text, or <see langword="null"/> when the body is not an <c>OperationOutcome</c> with an issue (an HTML page of a proxy, nothing).</returns>
    /// <example>
    /// <code>
    /// var text = FhirServerHttp.OutcomeText("""{ "resourceType": "OperationOutcome", "issue": [ { "severity": "error", "code": "invalid", "diagnostics": "Unknown search-param `date`." } ] }""");
    /// // Unknown search-param `date`.
    /// </code>
    /// </example>
    public static string? OutcomeText(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("resourceType", out var type) || type.ValueKind != JsonValueKind.String
                || type.GetString() != "OperationOutcome" || !root.TryGetProperty("issue", out var issues) || issues.ValueKind != JsonValueKind.Array
                || issues.GetArrayLength() == 0)
            {
                return null;
            }

            var all = issues.EnumerateArray().Where(issue => issue.ValueKind == JsonValueKind.Object).ToList();
            var issue = all.FirstOrDefault(i => Text(i, "severity") is "fatal" or "error");
            if (issue.ValueKind != JsonValueKind.Object)
            {
                issue = all.FirstOrDefault();
            }

            if (issue.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var text = Text(issue, "diagnostics")
                       ?? (issue.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object ? Text(details, "text") : null)
                       ?? Text(issue, "code");
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= OutcomeLength ? text : text[..(OutcomeLength - 1)] + "…";
        }
        catch (JsonException)
        {
            return null;
        }

        static string? Text(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }

    private const int OutcomeLength = 300;

    // The body of an error, as far as an OperationOutcome may go: a proxy may answer with a large page
    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var buffer = new char[64 * 1024];
            var read = await reader.ReadBlockAsync(buffer, cancellationToken);
            return new string(buffer, 0, read);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a JSON document (<c>Accept: application/fhir+json</c>) within a time (<see cref="TimeoutOf"/>).
    /// </summary>
    /// <param name="httpClientFactory">Makes the client <see cref="HttpClientName"/>.</param>
    /// <param name="url">The absolute address.</param>
    /// <param name="authorization">The value of the <c>Authorization</c> header (<c>Bearer ...</c>), or <see langword="null"/> for none.</param>
    /// <param name="timeout">How long the request may take until the answer starts.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The document; the caller disposes it.</returns>
    /// <exception cref="HttpRequestException">The server did not answer; an error status is a <see cref="FhirRequestException"/> with the status
    /// (<see cref="HttpRequestException.StatusCode"/>) and what the server said (<see cref="FhirRequestException.Outcome"/>).</exception>
    /// <exception cref="TimeoutException">The server did not answer within <paramref name="timeout"/> (not a cancellation: the caller did not cancel).</exception>
    /// <exception cref="System.Text.Json.JsonException">The answer is not JSON.</exception>
    /// <example>
    /// <code>
    /// using var document = await FhirServerHttp.GetJsonAsync(factory, "https://hapi.fhir.org/baseR4/Patient/1", null, TimeSpan.FromSeconds(60), cancellationToken);
    /// </code>
    /// </example>
    public static async Task<JsonDocument> GetJsonAsync(IHttpClientFactory httpClientFactory, string url, string? authorization, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        using var http = httpClientFactory.CreateClient(HttpClientName);
        http.Timeout = timeout;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = ServerAccessExtensions.ToHeader(authorization);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.9));
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation; the callers would take it for one and drop the error
            throw new TimeoutException($"The server did not answer within {timeout.TotalSeconds:0} seconds: {new Uri(url).GetLeftPart(UriPartial.Path)}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var outcome = OutcomeText(await ReadErrorAsync(response, cancellationToken));
                throw new FhirRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {new Uri(url).GetLeftPart(UriPartial.Path)}{(outcome is null ? string.Empty : " - " + outcome)}",
                    outcome, response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
    }
}
