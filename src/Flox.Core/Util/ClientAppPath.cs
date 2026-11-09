using Microsoft.Extensions.Configuration;

namespace Flox.Core.Util;

// Where the browser client (Blazor WebAssembly) lives on the server: "/app" by default, so that "/" is free for the public site.
// The Flox:ClientApp:Path setting changes it; "/" (or an empty value) puts the client at the root, as before
public static class ClientAppPath
{
    public const string ConfigurationKey = "Flox:ClientApp:Path";
    public const string Default = "/app";

    // "/segment[/segment]" without the trailing slash, or an empty string for the root
    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[ConfigurationKey];
        return value is null ? Default : Normalize(value);
    }

    public static string Normalize(string? path)
    {
        var trimmed = path?.Trim()
                           .Trim('/');
        return string.IsNullOrEmpty(trimmed) ? string.Empty : "/" + trimmed;
    }
}
