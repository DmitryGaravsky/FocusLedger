namespace FocusLedger.Core.Signals;

// Declares whether bounded-pipeline saturation may coalesce a signal or must preserve it.
public enum SignalDelivery {
    NonDroppable,
    Coalescible
}

// Represents one observed platform condition before state transition and privacy processing.
public abstract record ActivitySignal(
    DateTimeOffset ObservedAt,
    long MonotonicTimestamp,
    SignalDelivery Delivery) {
    // Determines whether a pending coalescible observation already represents the same semantic update.
    public virtual bool CanCoalesceWith(ActivitySignal other) {
        return Equals(other);
    }
}
