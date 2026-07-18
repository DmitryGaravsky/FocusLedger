namespace FocusLedger.Core.State;

// Describes the effective presence change produced by one reconciled condition snapshot.
public sealed record PresenceTransition(
    PresenceState PreviousState,
    PresenceState CurrentState) {
    public bool Changed {
        get { return PreviousState != CurrentState; }
    }
}
