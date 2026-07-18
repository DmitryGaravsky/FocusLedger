namespace FocusLedger.Core.Events;

// Defines the stable ordering, identity, time, and type fields shared by every persisted event.
public sealed record EventEnvelope(
    int SchemaVersion,
    long Sequence,
    Guid EventId,
    DateTimeOffset TimestampUtc,
    int UtcOffsetMinutes,
    string Type);
