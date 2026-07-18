namespace FocusLedger.Core.State;

// Owns deterministic tracker lifecycle transitions for the serialized runtime coordinator.
public sealed class TrackerLifecycleStateMachine {
    TrackerLifecycleState state = TrackerLifecycleState.Starting;
    public TrackerLifecycleState State {
        get { return state; }
    }
    // Completes initialization and restores the user's persisted manual-pause choice.
    public TrackerLifecycleTransition CompleteStartup(bool restorePaused) {
        TrackerLifecycleState targetState = restorePaused
            ? TrackerLifecycleState.Paused
            : TrackerLifecycleState.Running;
        return TransitionFrom(TrackerLifecycleState.Starting, targetState);
    }
    // Suspends attribution while keeping operational presence observation active.
    public TrackerLifecycleTransition Pause() {
        return TransitionFrom(TrackerLifecycleState.Running, TrackerLifecycleState.Paused);
    }
    // Resumes attribution after an explicit user command.
    public TrackerLifecycleTransition Resume() {
        return TransitionFrom(TrackerLifecycleState.Paused, TrackerLifecycleState.Running);
    }
    // Starts graceful shutdown from any non-terminal operational phase.
    public TrackerLifecycleTransition BeginStopping() {
        if(State == TrackerLifecycleState.Stopping) {
            return Unchanged();
        }
        if(State is TrackerLifecycleState.Starting or
            TrackerLifecycleState.Running or
            TrackerLifecycleState.Paused) {
            return TransitionTo(TrackerLifecycleState.Stopping);
        }
        throw InvalidTransition(TrackerLifecycleState.Stopping);
    }
    // Marks deterministic shutdown completion after all owned resources have stopped.
    public TrackerLifecycleTransition CompleteStopping() {
        return TransitionFrom(TrackerLifecycleState.Stopping, TrackerLifecycleState.Stopped);
    }
    // Records an unrecoverable coordinator or persistence failure.
    public TrackerLifecycleTransition Fault() {
        if(State == TrackerLifecycleState.Faulted) {
            return Unchanged();
        }
        if(State is TrackerLifecycleState.Starting or
            TrackerLifecycleState.Running or
            TrackerLifecycleState.Paused or
            TrackerLifecycleState.Stopping) {
            return TransitionTo(TrackerLifecycleState.Faulted);
        }
        throw InvalidTransition(TrackerLifecycleState.Faulted);
    }
    TrackerLifecycleTransition TransitionFrom(TrackerLifecycleState requiredState, TrackerLifecycleState targetState) {
        if(State == targetState) {
            return Unchanged();
        }
        if(State != requiredState) {
            throw InvalidTransition(targetState);
        }
        return TransitionTo(targetState);
    }
    TrackerLifecycleTransition TransitionTo(TrackerLifecycleState targetState) {
        TrackerLifecycleState previousState = State;
        state = targetState;
        return new TrackerLifecycleTransition(previousState, State);
    }
    TrackerLifecycleTransition Unchanged() {
        return new TrackerLifecycleTransition(State, State);
    }
    InvalidOperationException InvalidTransition(TrackerLifecycleState targetState) {
        return new InvalidOperationException($"Tracker lifecycle transition from {State} to {targetState} is not allowed.");
    }
}
