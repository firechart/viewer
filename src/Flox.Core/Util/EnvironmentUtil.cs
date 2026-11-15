namespace Flox.Core.Util;

/// <summary>
/// Helpers for the process environment.
/// </summary>
public static class EnvironmentUtil
{
    /// <summary>
    /// Returns the directory of the application.
    /// </summary>
    public static string GetBaseDirectory()
    {
        return AppContext.BaseDirectory;
    }
}
