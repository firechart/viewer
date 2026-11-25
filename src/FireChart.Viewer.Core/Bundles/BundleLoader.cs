using FireChart.Viewer.Core.Fhir;

namespace FireChart.Viewer.Core.Bundles;

// A Bundle read by its address (a search, $everything, a document): relative to the server when there is one, and sent with the credentials of the
// server only to the origin of the server; an address elsewhere is read without them
internal static class BundleLoader
{
    public static string AddressOf(string location, IViewerServer? server)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        var text = location.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            return absolute.AbsoluteUri;
        }

        if (server is null)
        {
            throw new FormatException("A relative address needs a server.");
        }

        return $"{server.BaseUrl.TrimEnd('/')}/{text.TrimStart('/')}";
    }

    // Whether the address goes to the server, with its credentials (its origin)
    public static bool IsOfServer(string location, IViewerServer? server)
    {
        return server is not null && SameOrigin(server.BaseUrl, AddressOf(location, server));
    }

    public static async Task<string> LoadAsync(IHttpClientFactory httpClientFactory, IServerCredentialStore credentials, string location, IViewerServer? server,
                                               CancellationToken cancellationToken)
    {
        var url = AddressOf(location, server);
        string? authorization = null;
        var timeout = TimeSpan.FromSeconds(FhirServerHttp.DefaultTimeoutSeconds);
        if (server is not null && IsOfServer(location, server))
        {
            authorization = credentials.GetAuthorization(server);
            timeout = FhirServerHttp.TimeoutOf(server);
        }

        using var document = await FhirServerHttp.GetJsonAsync(httpClientFactory, url, authorization, timeout, cancellationToken);
        return document.RootElement.GetRawText();
    }

    private static bool SameOrigin(string baseUrl, string url)
    {
        try
        {
            FhirServerReader.EnsureSameOrigin(baseUrl, url);
            return true;
        }
        catch (Flox.Core.FloxException)
        {
            return false;
        }
    }
}
