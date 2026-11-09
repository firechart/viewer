using System.Text.Json;

namespace Flox.Core.Util;

public static class FileUtil
{
    public static T? TryLoadAndDeserialize<T>(string fileName, params string[] probePaths) where T : class
    {
        foreach (var path in probePaths)
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
            {
                using var stream = File.OpenRead(fullPath);
                return JsonSerializer.Deserialize<T>(stream, JsonUtil.DefaultCamelCaseOptions);
            }
        }

        return null;
    }
}
