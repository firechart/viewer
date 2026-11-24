using Microsoft.Extensions.Configuration;

namespace Flox.Core.Util;

/// <summary>
/// Where the browser client (Blazor WebAssembly) lives on the server: "/app" by default, so that "/" is free for the public site. Flox:ClientApp:Path changes it without a new build (the server sets the path base the client takes its base address from); "/" (or an empty value) puts the client at the root.
/// </summary>
public static class ClientAppPath
{
    /// <summary>
    /// Configuration key of the path of the client.
    /// </summary>
    public const string ConfigurationKey = "Flox:ClientApp:Path";
    /// <summary>
    /// The default path of the client.
    /// </summary>
    public const string Default = "/app";

    /// <summary>
    /// Returns the path of the client from the configuration: "/segment[/segment]" without the trailing slash, or an empty string for the root.
    /// </summary>
    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[ConfigurationKey];
        return value is null ? Default : Normalize(value);
    }

    /// <summary>
    /// Brings a path to the form "/segment[/segment]" without the trailing slash, or an empty string for the root.
    /// </summary>
    public static string Normalize(string? path)
    {
        var trimmed = path?.Trim()
                           .Trim('/');
        return string.IsNullOrEmpty(trimmed) ? string.Empty : "/" + trimmed;
    }
}
