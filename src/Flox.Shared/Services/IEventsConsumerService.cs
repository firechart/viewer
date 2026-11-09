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

    bool IsConnected { get; }

    /// <summary>
    /// (Re)opens the connection for the current user and closes it when the user is not authenticated.
    /// Call again after the authentication state changes.
    /// </summary>
    Task SubscribeAsync(CancellationToken cancellationToken = default);
}
