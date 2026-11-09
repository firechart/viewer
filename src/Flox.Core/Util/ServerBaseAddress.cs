using Microsoft.Extensions.Configuration;

namespace Flox.Core.Util;

// The address of the application server as the clients see it. The Flox:Bootstrap:BaseAddress setting sets it explicitly (MAUI and desktop have to,
// a WASM client may to talk to another server); without it the address the client came from is used (WASM: the host that
// served the page)
public static class ServerBaseAddress
{
    public const string ConfigurationKey = "Flox:Bootstrap:BaseAddress";

    // Without the trailing slash, so callers append their own "/path"
    public static string Resolve(IConfiguration configuration, string? fallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var address = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(address))
        {
            // The page of a WASM client is below the client's path, the server is the whole origin
            address = Uri.TryCreate(fallback, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : fallback;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new FloxException($"The server address is not configured ({ConfigurationKey})");
        }

        return address.TrimEnd('/');
    }
}
