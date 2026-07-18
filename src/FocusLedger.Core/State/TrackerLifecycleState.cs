namespace FocusLedger.Core.State;

// Identifies the process-level lifecycle phase independently from presence and activity context.
public enum TrackerLifecycleState {
    Starting,
    Running,
    Paused,
    Stopping,
    Stopped,
    Faulted
}
