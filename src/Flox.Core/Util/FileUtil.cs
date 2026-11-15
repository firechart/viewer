using System.Text.Json;

namespace Flox.Core.Util;

/// <summary>
/// Helpers for files.
/// </summary>
public static class FileUtil
{
    /// <summary>
    /// Reads a JSON file from the first of the directories that has it and returns its content, or null when none has.
    /// </summary>
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
