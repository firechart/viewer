namespace Flox.Shared.Services;

/// <summary>
/// Addresses of the server push channel (a SignalR hub), shared by the server and its clients.
/// </summary>
public static class EventsEndpoints
{
    public const string Hub = "/hubs/events";

    /// <summary>Client method the server invokes with a <see cref="EventMessage"/>.</summary>
    public const string MessageMethod = "message";
}
