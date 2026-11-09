using System.Reflection;

namespace Flox.Core.Util;

public static class ResourceUtil
{
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
