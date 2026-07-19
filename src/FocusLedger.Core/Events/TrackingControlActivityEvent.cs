using System.Text.Json.Serialization;

namespace FocusLedger.Core.Events;

// Represents an explicit user pause or resume without storing any foreground context.
public sealed record TrackingControlActivityEvent : ActivityEvent {
    [JsonConstructor]
    public TrackingControlActivityEvent(
        int schemaVersion,
        long sequence,
        Guid eventId,
        DateTimeOffset timestampUtc,
        int utcOffsetMinutes,
        string type,
        string? source,
        Guid? correlationId)
        : base(new EventEnvelope(schemaVersion, sequence, eventId, timestampUtc, utcOffsetMinutes, type, source, correlationId)) {
        if(type is not "tracking.paused" and not "tracking.resumed")
            throw new ArgumentException("A tracking control event requires a canonical pause or resume type.", nameof(type));
    }
    public TrackingControlActivityEvent(EventEnvelope envelope)
        : this(envelope.SchemaVersion, envelope.Sequence, envelope.EventId, envelope.TimestampUtc, envelope.UtcOffsetMinutes,
            envelope.Type, envelope.Source, envelope.CorrelationId) {
    }
    [JsonPropertyOrder(0)]
    public int SchemaVersion {
        get { return Envelope.SchemaVersion; }
    }
    [JsonPropertyOrder(1)]
    public long Sequence {
        get { return Envelope.Sequence; }
    }
    [JsonPropertyOrder(2)]
    public Guid EventId {
        get { return Envelope.EventId; }
    }
    [JsonPropertyOrder(3)]
    public DateTimeOffset TimestampUtc {
        get { return Envelope.TimestampUtc; }
    }
    [JsonPropertyOrder(4)]
    public int UtcOffsetMinutes {
        get { return Envelope.UtcOffsetMinutes; }
    }
    [JsonPropertyOrder(5)]
    public string Type {
        get { return Envelope.Type; }
    }
    [JsonPropertyOrder(6)]
    public string? Source {
        get { return Envelope.Source; }
    }
    [JsonPropertyOrder(7)]
    public Guid? CorrelationId {
        get { return Envelope.CorrelationId; }
    }
}
