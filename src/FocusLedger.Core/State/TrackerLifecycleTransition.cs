namespace FocusLedger.Core.State;

// Describes an accepted lifecycle command so callers can emit events only for semantic changes.
public sealed record TrackerLifecycleTransition(
    TrackerLifecycleState PreviousState,
    TrackerLifecycleState CurrentState) {
    public bool Changed {
        get { return PreviousState != CurrentState; }
    }
}
