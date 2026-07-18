using FocusLedger.Core.State;

namespace FocusLedger.Core.Coordination;

// Groups the mutable state machines that may be changed only by the serialized coordinator consumer.
public sealed class ActivityRuntimeState {
    public TrackerLifecycleStateMachine TrackerLifecycle { get; } = new();
    public PresenceStateMachine Presence { get; } = new();
}
