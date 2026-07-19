namespace FocusLedger.Core.Signals;

// Distinguishes a foreground switch from a title-change candidate requiring later reconciliation.
public enum ForegroundObservationKind {
    WindowChanged,
    TitleChangedCandidate
}

// Carries only an opaque top-level window identifier from a non-blocking platform callback.
public sealed record ForegroundWindowSignal(
    long WindowHandle,
    ForegroundObservationKind Kind,
    DateTimeOffset ObservedAt,
    long MonotonicTimestamp,
    SignalDelivery Delivery) : ActivitySignal(ObservedAt, MonotonicTimestamp, Delivery) {
    public override bool CanCoalesceWith(ActivitySignal other) {
        return Kind == ForegroundObservationKind.TitleChangedCandidate
            && other is ForegroundWindowSignal candidate
            && candidate.Kind == Kind
            && candidate.WindowHandle == WindowHandle;
    }
}
