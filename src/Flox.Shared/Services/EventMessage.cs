using System.Text.Json;

namespace Flox.Shared.Services;

/// <summary>
/// Envelope of a message pushed from the server to a client: a JSON-serialized payload and its CLR type name.
/// </summary>
public class EventMessage : GuidEntity
{
    /// <summary>Creates an empty message.</summary>
    public EventMessage()
    {
    }

    /// <summary>Creates a message that carries <paramref name="payload"/>.</summary>
    public EventMessage(object? payload)
    {
        Serialize(payload);
    }

    /// <summary>Stores the payload as JSON together with its CLR type name.</summary>
    public void Serialize(object? payload)
    {
        if (payload == null)
        {
            Payload = null;
            PayloadType = null;
        }
        else
        {
            Payload = JsonSerializer.Serialize(payload, JsonUtil.DefaultCamelCaseOptions);
            if (Payload == null)
            {
                PayloadType = null;
            }
            else
            {
                PayloadType = payload.GetType()
                                     .FullName ?? throw new InvalidOperationException();
            }
        }
    }

    /// <summary>Reads the payload as an object of the given type.</summary>
    public object? Deserialize(Type type)
    {
        if (Payload == null)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(type);

        var result = JsonSerializer.Deserialize(Payload, type, JsonUtil.DefaultCamelCaseOptions);
        return result;
    }

    /// <summary>Reads the payload as <typeparamref name="T"/>.</summary>
    public T? Deserialize<T>()
    {
        return (T?)Deserialize(typeof(T));
    }

    /// <summary>Event type of a message that does not set one.</summary>
    public const string DefaultEventType = "general";

    /// <summary>Application-level event type; not the name of the SignalR client method.</summary>
    public string EventType { get; set; } = DefaultEventType;

    /// <summary>The payload as JSON.</summary>
    public string? Payload { get; set; }

    /// <summary>Full CLR type name of the payload; consumers match on it before deserializing.</summary>
    public string? PayloadType { get; set; }

    /// <summary>The <c>sub</c> of the addressee; <c>null</c> for a broadcast.</summary>
    public string? Receiver { get; set; }

    /// <summary>Whether the message is for every user rather than for one addressee.</summary>
    public bool IsBroadcast
    {
        get
        {
            return Receiver is null;
        }
    }
}
