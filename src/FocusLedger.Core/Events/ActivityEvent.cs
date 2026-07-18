namespace FocusLedger.Core.Events;

// Represents one privacy-normalized state transition ready for append-only persistence.
public abstract record ActivityEvent(EventEnvelope Envelope);
