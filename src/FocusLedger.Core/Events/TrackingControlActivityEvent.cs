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
}
