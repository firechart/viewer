using System.Text.Json;

namespace Flox.Shared.Services;

/// <summary>
/// Envelope of a message pushed from the server to a client: a JSON-serialized payload and its CLR type name.
/// </summary>
public class EventMessage : GuidEntity
{
    public EventMessage()
    {
    }

    public EventMessage(object? payload)
    {
        Serialize(payload);
    }

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

    public T? Deserialize<T>()
    {
        return (T?)Deserialize(typeof(T));
    }

    /// <summary>Event type of a message that does not set one.</summary>
    public const string DefaultEventType = "general";

    /// <summary>Application-level event type; not the name of the SignalR client method.</summary>
    public string EventType { get; set; } = DefaultEventType;

    public string? Payload { get; set; }

    /// <summary>Full CLR type name of the payload; consumers match on it before deserializing.</summary>
    public string? PayloadType { get; set; }

    /// <summary>The <c>sub</c> of the addressee; <c>null</c> for a broadcast.</summary>
    public string? Receiver { get; set; }

    public bool IsBroadcast
    {
        get
        {
            return Receiver is null;
        }
    }
}
