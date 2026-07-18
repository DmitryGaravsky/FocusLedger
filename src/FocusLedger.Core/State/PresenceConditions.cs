namespace FocusLedger.Core.State;

// Carries one immutable reconciliation snapshot without retaining platform-specific or personal data.
public sealed record PresenceConditions(
    PresenceActivityState Activity,
    bool IsSessionLocked,
    bool IsSessionDisconnected,
    bool IsSystemSuspended);
