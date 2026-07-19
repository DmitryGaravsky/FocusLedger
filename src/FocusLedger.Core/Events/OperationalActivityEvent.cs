using System.Text.Json.Serialization;

namespace FocusLedger.Core.Events;

// Represents a compact tracker lifecycle or heartbeat event with no inferred activity payload.
public sealed record OperationalActivityEvent : ActivityEvent {
    [JsonConstructor]
    public OperationalActivityEvent(
        int schemaVersion,
        long sequence,
        Guid eventId,
        DateTimeOffset timestampUtc,
        int utcOffsetMinutes,
        string type,
        string? source,
        Guid? correlationId)
        : base(new EventEnvelope(schemaVersion, sequence, eventId, timestampUtc, utcOffsetMinutes, type, source, correlationId)) {
        if(type is not "tracker.started" and not "tracker.recovered_after_unclean_shutdown" and not "heartbeat")
            throw new ArgumentException("An operational event requires a canonical startup, recovery, or heartbeat type.", nameof(type));
    }
    public OperationalActivityEvent(EventEnvelope envelope)
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
