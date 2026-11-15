using Microsoft.Extensions.Configuration;

namespace Flox.Core.Util;

/// <summary>
/// Where the browser client (Blazor WebAssembly) lives on the server: "/app" by default, so that "/" is free for the public site. Flox:ClientApps:App:Path changes it; "/" (or an empty value) puts the client at the root. Flox:ClientApps holds every browser application of the server by its name.
/// </summary>
public static class ClientAppPath
{
    /// <summary>
    /// Configuration section that holds the browser applications.
    /// </summary>
    public const string Section = "Flox:ClientApps";
    /// <summary>
    /// Name of the default browser application.
    /// </summary>
    public const string AppName = "App";
    /// <summary>
    /// Configuration key of the path of the default application.
    /// </summary>
    public const string ConfigurationKey = Section + ":" + AppName + ":Path";
    /// <summary>
    /// Configuration key of the host of the default application.
    /// </summary>
    public const string HostKey = Section + ":" + AppName + ":Host";
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
