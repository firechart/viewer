using System.Net.Sockets;

namespace Flox.Shared.Errors;

/// <summary>Recognizes failures of the connection to a server.</summary>
public static class ConnectionErrors
{
    /// <summary>Tells whether an exception, or any exception it wraps, is a network failure (<see cref="HttpRequestException"/> or <see cref="SocketException"/>).</summary>
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
