using System.Net.Sockets;

namespace Flox.Shared.Errors;

public static class ConnectionErrors
{
    public static bool IsConnectionLost(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException or SocketException)
            {
                return true;
            }
        }

        return false;
    }
}
