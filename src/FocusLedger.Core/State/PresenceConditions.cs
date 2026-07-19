namespace FocusLedger.Core.State;

// Represents the reconciled input-activity condition before session and power precedence is applied.
public enum PresenceActivityState {
    Unknown,
    Active,
    Idle
}

// Carries one immutable reconciliation snapshot without retaining platform-specific or personal data.
public sealed record PresenceConditions(
    PresenceActivityState Activity,
    bool IsSessionLocked,
    bool IsSessionDisconnected,
    bool IsSystemSuspended);
