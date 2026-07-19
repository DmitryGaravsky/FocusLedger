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

// Carries a resolved input-activity transition without collecting any input content or input type.
public sealed record PresenceActivitySignal(
    State.PresenceActivityState Activity,
    TimeSpan IdleThreshold,
    DateTimeOffset ObservedAt,
    long MonotonicTimestamp) : ActivitySignal(ObservedAt, MonotonicTimestamp, SignalDelivery.NonDroppable);

// Identifies a session boundary without retaining a user, machine, or remote endpoint identity.
public enum SessionActivityKind {
    ConsoleConnected,
    ConsoleDisconnected,
    RemoteConnected,
    RemoteDisconnected,
    Logon,
    Logoff,
    Locked,
    Unlocked
}

// Carries one current-session transition from the WTS message boundary to the serialized coordinator.
public sealed record SessionActivitySignal(
    SessionActivityKind Activity,
    DateTimeOffset ObservedAt,
    long MonotonicTimestamp) : ActivitySignal(ObservedAt, MonotonicTimestamp, SignalDelivery.NonDroppable);

// Identifies the system-power boundary that controls activity attribution and collector reconciliation.
public enum SystemPowerActivityKind {
    Suspending,
    Resumed
}

// Carries a critical suspend or resume transition to the serialized coordinator and event writer.
public sealed record SystemPowerActivitySignal(
    SystemPowerActivityKind Activity,
    DateTimeOffset ObservedAt,
    long MonotonicTimestamp) : ActivitySignal(ObservedAt, MonotonicTimestamp, SignalDelivery.NonDroppable);

public enum OperationalActivitySignalKind {
    TrackerStarted,
    RecoveredAfterUncleanShutdown,
    Heartbeat
}

// Requests one lifecycle or liveness event without carrying reconstructed activity for an unobserved gap.
public sealed record OperationalActivitySignal(
    OperationalActivitySignalKind Activity,
    DateTimeOffset ObservedAt,
    long MonotonicTimestamp) : ActivitySignal(
        ObservedAt,
        MonotonicTimestamp,
        Activity == OperationalActivitySignalKind.Heartbeat ? SignalDelivery.Coalescible : SignalDelivery.NonDroppable) {
    public override bool CanCoalesceWith(ActivitySignal other) {
        return other is OperationalActivitySignal { Activity: OperationalActivitySignalKind.Heartbeat };
    }
}
