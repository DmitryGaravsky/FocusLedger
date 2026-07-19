namespace FocusLedger.Core.Events;

using System.Text.Json.Serialization;

// Represents one privacy-normalized state transition ready for append-only persistence.
public abstract record ActivityEvent([property: JsonIgnore] EventEnvelope Envelope);
