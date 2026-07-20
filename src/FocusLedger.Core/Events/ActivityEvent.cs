namespace FocusLedger.Core.Events;

using System.Text.Json.Serialization;

// Represents one privacy-normalized state transition ready for append-only persistence.
public abstract record ActivityEvent([property: JsonIgnore] EventEnvelope Envelope) {
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
