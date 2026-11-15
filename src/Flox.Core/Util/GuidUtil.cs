using System.Globalization;

namespace Flox.Core.Util;

/// <summary>
/// Helpers for identifiers.
/// </summary>
public static class GuidUtil
{
    /// <summary>
    /// Creates a new time-ordered (version 7) identifier.
    /// </summary>
    public static Guid CreateUuid()
    {
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Creates a new time-ordered identifier as a lower-case string.
    /// </summary>
    public static string CreateStringUuid()
    {
        return CreateUuid()
              .ToString()
              .ToLower(CultureInfo.InvariantCulture);
    }
}
