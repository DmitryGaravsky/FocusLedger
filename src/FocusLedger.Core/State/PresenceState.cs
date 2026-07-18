namespace FocusLedger.Core.State;

// Identifies the single effective presence state used for activity attribution.
public enum PresenceState {
    Unknown,
    Active,
    Idle,
    SessionLocked,
    SessionDisconnected,
    SystemSuspended
}
