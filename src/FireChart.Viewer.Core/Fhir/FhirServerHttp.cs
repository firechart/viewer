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
    /// Gets a JSON document (<c>Accept: application/fhir+json</c>).
    /// </summary>
    /// <param name="httpClientFactory">Makes the client <see cref="HttpClientName"/>.</param>
    /// <param name="url">The absolute address.</param>
    /// <param name="authorization">The value of the <c>Authorization</c> header (<c>Bearer ...</c>), or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The document; the caller disposes it.</returns>
    /// <exception cref="HttpRequestException">The server did not answer, or answered with an error status (<see cref="HttpRequestException.StatusCode"/>).</exception>
    /// <exception cref="System.Text.Json.JsonException">The answer is not JSON.</exception>
    public static async Task<JsonDocument> GetJsonAsync(IHttpClientFactory httpClientFactory, string url, string? authorization, CancellationToken cancellationToken)
    {
        using var http = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = ServerAccessExtensions.ToHeader(authorization);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.9));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
