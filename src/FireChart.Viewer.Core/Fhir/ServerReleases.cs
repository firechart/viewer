using Flox.Core;
using Hl7.Fhir.Utility;

namespace FireChart.Viewer.Core.Fhir;

/// <summary>
/// The release of a server.
/// </summary>
public static class ServerReleases
{
    /// <summary>
    /// What the server says about itself (its <c>CapabilityStatement</c>, one request for the session), else <see cref="IViewerServer.FhirRelease"/>.
    /// </summary>
    /// <param name="types">The provider of the resource types of the servers.</param>
    /// <param name="server">The server.</param>
    /// <returns>The release (<c>R4</c>), or <see langword="null"/> when neither the server nor its record says.</returns>
    /// <example>
    /// <code>
    /// var release = await types.ReleaseOfAsync(server) ?? "R4";
    /// </code>
    /// </example>
    public static async Task<string?> ReleaseOfAsync(this IServerResourceTypeProvider types, IViewerServer server)
    {
        try
        {
            return (await types.GetAsync(server)).Release;
        }
        catch (Exception ex) when (ex is HttpRequestException or FloxException or System.Text.Json.JsonException)
        {
            return server.FhirRelease?.GetLiteral();
        }
    }
}
