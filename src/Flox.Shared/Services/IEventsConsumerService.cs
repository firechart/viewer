namespace Flox.Shared.Services;

/// <summary>
/// Client of the server push channel of the current user.
/// </summary>
public interface IEventsConsumerService
{
    /// <summary>
    /// Raised for every received <see cref="EventMessage"/>; handler exceptions are logged and swallowed.
    /// </summary>
    event EventHandler<GenericEventArgs<EventMessage>>? OnMessage;

    /// <summary>Whether the connection to the push channel is open.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Opens the connection for the current user (none when the user is not authenticated) and from then
    /// on follows the authentication state: signing in connects, signing out closes.
    /// </summary>
    Task SubscribeAsync(CancellationToken cancellationToken = default);
}
