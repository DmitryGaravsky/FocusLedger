namespace FocusLedger.Core.Signals;

// Represents one observed platform condition before state transition and privacy processing.
public abstract record ActivitySignal(
    DateTimeOffset ObservedAt,
    long MonotonicTimestamp,
    SignalDelivery Delivery);
