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

// Describes the effective presence change produced by one reconciled condition snapshot.
public sealed record PresenceTransition(
    PresenceState PreviousState,
    PresenceState CurrentState) {
    public bool Changed {
        get { return PreviousState != CurrentState; }
    }
}

// Resolves mutually exclusive presence attribution using the documented suppression precedence.
public sealed class PresenceStateMachine {
    PresenceState state = PresenceState.Unknown;
    public PresenceState State {
        get { return state; }
    }
    // Applies a complete observation snapshot so clearing a higher state restores the resolved lower state.
    public PresenceTransition Apply(PresenceConditions conditions) {
        ArgumentNullException.ThrowIfNull(conditions);
        PresenceState resolvedState = Resolve(conditions);
        PresenceTransition transition = new(state, resolvedState);
        state = resolvedState;
        return transition;
    }
    static PresenceState Resolve(PresenceConditions conditions) {
        if(conditions.IsSystemSuspended)
            return PresenceState.SystemSuspended;
        if(conditions.IsSessionDisconnected)
            return PresenceState.SessionDisconnected;
        if(conditions.IsSessionLocked)
            return PresenceState.SessionLocked;
        return conditions.Activity switch {
            PresenceActivityState.Active => PresenceState.Active,
            PresenceActivityState.Idle => PresenceState.Idle,
            PresenceActivityState.Unknown => PresenceState.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(conditions), conditions.Activity, "Presence activity state is not supported.")
        };
    }
}
