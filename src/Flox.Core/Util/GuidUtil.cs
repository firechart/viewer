using System.Globalization;

namespace Flox.Core.Util;

public static class GuidUtil
{
    public static Guid CreateUuid()
    {
        return Guid.CreateVersion7();
    }

    public static string CreateStringUuid()
    {
        return CreateUuid()
              .ToString()
              .ToLower(CultureInfo.InvariantCulture);
    }
}
