using System.Text.Json.Serialization;

namespace FocusLedger.Core.Events;

// Represents a schema-1 day boundary with no payload beyond the flattened common envelope.
public sealed record DayBoundaryActivityEvent : ActivityEvent {
    [JsonConstructor]
    public DayBoundaryActivityEvent(
        int schemaVersion,
        long sequence,
        Guid eventId,
        DateTimeOffset timestampUtc,
        int utcOffsetMinutes,
        string type,
        string? source,
        Guid? correlationId)
        : base(new EventEnvelope(schemaVersion, sequence, eventId, timestampUtc, utcOffsetMinutes, type, source, correlationId)) {
        if(type is not "day.started" and not "day.ended")
            throw new ArgumentException("A day boundary requires a canonical day event type.", nameof(type));
    }
    public DayBoundaryActivityEvent(EventEnvelope envelope)
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

// Stores the normalized meeting state without a title, participant, or provider-specific identifier.
public sealed record MeetingSnapshotEventData(string State);

// Stores the last privacy-safe foreground attribution copied into a daily snapshot.
public sealed record ForegroundSnapshotEventData(
    ApplicationEventData App,
    ContextEventData? Context,
    ClassificationEventData Classification);

// Captures the complete privacy-safe coordinator state needed to analyze a daily file independently.
public sealed record StateSnapshotActivityEvent : ActivityEvent {
    [JsonConstructor]
    public StateSnapshotActivityEvent(
        int schemaVersion,
        long sequence,
        Guid eventId,
        DateTimeOffset timestampUtc,
        int utcOffsetMinutes,
        string type,
        string? source,
        Guid? correlationId,
        string trackerState,
        string presence,
        MeetingSnapshotEventData meeting,
        ForegroundSnapshotEventData? foreground)
        : base(new EventEnvelope(schemaVersion, sequence, eventId, timestampUtc, utcOffsetMinutes, type, source, correlationId)) {
        if(type != "state.snapshot")
            throw new ArgumentException("A state snapshot requires the canonical state.snapshot event type.", nameof(type));
        ArgumentException.ThrowIfNullOrWhiteSpace(trackerState);
        ArgumentException.ThrowIfNullOrWhiteSpace(presence);
        ArgumentNullException.ThrowIfNull(meeting);
        TrackerState = trackerState;
        Presence = presence;
        Meeting = meeting;
        Foreground = foreground;
    }
    public StateSnapshotActivityEvent(
        EventEnvelope envelope,
        string trackerState,
        string presence,
        MeetingSnapshotEventData meeting,
        ForegroundSnapshotEventData? foreground)
        : this(envelope.SchemaVersion, envelope.Sequence, envelope.EventId, envelope.TimestampUtc, envelope.UtcOffsetMinutes,
            envelope.Type, envelope.Source, envelope.CorrelationId, trackerState, presence, meeting, foreground) {
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
    [JsonPropertyOrder(8)]
    public string TrackerState { get; }
    [JsonPropertyOrder(9)]
    public string Presence { get; }
    [JsonPropertyOrder(10)]
    public MeetingSnapshotEventData Meeting { get; }
    [JsonPropertyOrder(11)]
    public ForegroundSnapshotEventData? Foreground { get; }
}
