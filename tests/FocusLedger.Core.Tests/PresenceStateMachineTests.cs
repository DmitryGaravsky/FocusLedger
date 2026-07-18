using FocusLedger.Core.State;

namespace FocusLedger.Core.Tests;

public sealed class PresenceStateMachineTests {
    [TestCase(PresenceActivityState.Unknown, PresenceState.Unknown)]
    [TestCase(PresenceActivityState.Active, PresenceState.Active)]
    [TestCase(PresenceActivityState.Idle, PresenceState.Idle)]
    public void ActivityConditionResolvesWithoutSessionSuppression(PresenceActivityState activity, PresenceState expectedState) {
        PresenceStateMachine stateMachine = new();
        PresenceTransition transition = stateMachine.Apply(new PresenceConditions(activity, false, false, false));
        Assert.Multiple(() => {
            Assert.That(transition.PreviousState, Is.EqualTo(PresenceState.Unknown));
            Assert.That(transition.CurrentState, Is.EqualTo(expectedState));
            Assert.That(transition.Changed, Is.EqualTo(expectedState != PresenceState.Unknown));
            Assert.That(stateMachine.State, Is.EqualTo(expectedState));
        });
    }
    [TestCase(false, false, false, PresenceState.Idle)]
    [TestCase(true, false, false, PresenceState.SessionLocked)]
    [TestCase(true, true, false, PresenceState.SessionDisconnected)]
    [TestCase(true, true, true, PresenceState.SystemSuspended)]
    [TestCase(false, true, true, PresenceState.SystemSuspended)]
    [TestCase(true, false, true, PresenceState.SystemSuspended)]
    public void HigherPresenceConditionSuppressesLowerConditions(
        bool isLocked,
        bool isDisconnected,
        bool isSuspended,
        PresenceState expectedState) {
        PresenceStateMachine stateMachine = new();
        PresenceConditions conditions = new(PresenceActivityState.Idle, isLocked, isDisconnected, isSuspended);
        stateMachine.Apply(conditions);
        Assert.That(stateMachine.State, Is.EqualTo(expectedState));
    }
    [Test]
    public void ReconciliationRestoresLowerStateAfterSuppressionClears() {
        PresenceStateMachine stateMachine = new();
        stateMachine.Apply(new PresenceConditions(PresenceActivityState.Idle, true, true, true));
        PresenceTransition disconnected = stateMachine.Apply(new PresenceConditions(PresenceActivityState.Idle, true, true, false));
        PresenceTransition locked = stateMachine.Apply(new PresenceConditions(PresenceActivityState.Idle, true, false, false));
        PresenceTransition idle = stateMachine.Apply(new PresenceConditions(PresenceActivityState.Idle, false, false, false));
        Assert.Multiple(() => {
            Assert.That(disconnected.CurrentState, Is.EqualTo(PresenceState.SessionDisconnected));
            Assert.That(locked.CurrentState, Is.EqualTo(PresenceState.SessionLocked));
            Assert.That(idle.CurrentState, Is.EqualTo(PresenceState.Idle));
            Assert.That(stateMachine.State, Is.EqualTo(PresenceState.Idle));
        });
    }
    [Test]
    public void ResumeSnapshotCanEnterUnknownUntilCollectorsReconcile() {
        PresenceStateMachine stateMachine = new();
        stateMachine.Apply(new PresenceConditions(PresenceActivityState.Active, false, false, true));
        PresenceTransition transition = stateMachine.Apply(new PresenceConditions(PresenceActivityState.Unknown, false, false, false));
        Assert.Multiple(() => {
            Assert.That(transition.PreviousState, Is.EqualTo(PresenceState.SystemSuspended));
            Assert.That(transition.CurrentState, Is.EqualTo(PresenceState.Unknown));
            Assert.That(transition.Changed, Is.True);
        });
    }
    [Test]
    public void RepeatedSnapshotDoesNotProduceSemanticChange() {
        PresenceStateMachine stateMachine = new();
        PresenceConditions conditions = new(PresenceActivityState.Active, false, false, false);
        stateMachine.Apply(conditions);
        PresenceTransition transition = stateMachine.Apply(conditions);
        Assert.That(transition.Changed, Is.False);
    }
    [Test]
    public void UnsupportedActivityValueIsRejectedWithoutChangingState() {
        PresenceStateMachine stateMachine = new();
        PresenceConditions conditions = new((PresenceActivityState)int.MaxValue, false, false, false);
        Assert.Throws<ArgumentOutOfRangeException>(() => stateMachine.Apply(conditions));
        Assert.That(stateMachine.State, Is.EqualTo(PresenceState.Unknown));
    }
}
