using System.Text.Json.Serialization;

namespace FocusLedger.Core.Events;

// Stores only normalized application identity that is safe for the append-only activity stream.
public sealed record ApplicationEventData(string Id, string ProcessName, string Family);

// Stores an allowlisted context label and the privacy transformation that produced it.
public sealed record ContextEventData(string Label, string Privacy);

// Stores the deterministic and explainable result of local activity classification.
public sealed record ClassificationEventData(string Category, string Disposition, double Weight, string RuleId, double Confidence);

// Defines the frozen schema-1 foreground payload with a flattened common event envelope.
public sealed record ForegroundActivityEvent : ActivityEvent {
    [JsonConstructor]
    public ForegroundActivityEvent(
        int schemaVersion,
        long sequence,
        Guid eventId,
        DateTimeOffset timestampUtc,
        int utcOffsetMinutes,
        string type,
        string? source,
        Guid? correlationId,
        string presence,
        ApplicationEventData app,
        ContextEventData? context,
        ClassificationEventData classification)
        : base(new EventEnvelope(schemaVersion, sequence, eventId, timestampUtc, utcOffsetMinutes, type, source, correlationId)) {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(presence);
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(classification);
        Presence = presence;
        App = app;
        Context = context;
        Classification = classification;
    }
    public ForegroundActivityEvent(
        EventEnvelope envelope,
        string presence,
        ApplicationEventData app,
        ContextEventData? context,
        ClassificationEventData classification)
        : this(envelope.SchemaVersion, envelope.Sequence, envelope.EventId, envelope.TimestampUtc, envelope.UtcOffsetMinutes,
            envelope.Type, envelope.Source, envelope.CorrelationId, presence, app, context, classification) {
    }
    [JsonPropertyOrder(8)]
    public string Presence { get; }
    [JsonPropertyOrder(9)]
    public ApplicationEventData App { get; }
    [JsonPropertyOrder(10)]
    public ContextEventData? Context { get; }
    [JsonPropertyOrder(11)]
    public ClassificationEventData Classification { get; }
}
