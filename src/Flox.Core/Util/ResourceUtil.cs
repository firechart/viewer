using System.Reflection;

namespace Flox.Core.Util;

/// <summary>
/// Helpers for embedded resources.
/// </summary>
public static class ResourceUtil
{
    /// <summary>
    /// Returns an embedded resource as text, or null when there is none.
    /// </summary>
    public static string? LoadResourceAsString(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
