namespace Flox.Shared.Services;

/// <summary>The push channel of a client without an application server: never connected, no message.</summary>
public sealed class NoEventsConsumerService : IEventsConsumerService
{
#pragma warning disable CS0067
    /// <inheritdoc />
    public event EventHandler<GenericEventArgs<EventMessage>>? OnMessage;
#pragma warning restore CS0067

    /// <inheritdoc />
    public bool IsConnected
    {
        get
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task SubscribeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
