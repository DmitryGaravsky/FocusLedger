using FocusLedger.Core.State;

namespace FocusLedger.Core.Tests;

public sealed class TrackerLifecycleStateMachineTests {
    [TestCase(false, TrackerLifecycleState.Running)]
    [TestCase(true, TrackerLifecycleState.Paused)]
    public void CompleteStartupRestoresExpectedOperationalState(bool restorePaused, TrackerLifecycleState expectedState) {
        TrackerLifecycleStateMachine stateMachine = new();
        TrackerLifecycleTransition transition = stateMachine.CompleteStartup(restorePaused);
        Assert.Multiple(() => {
            Assert.That(transition.PreviousState, Is.EqualTo(TrackerLifecycleState.Starting));
            Assert.That(transition.CurrentState, Is.EqualTo(expectedState));
            Assert.That(transition.Changed, Is.True);
            Assert.That(stateMachine.State, Is.EqualTo(expectedState));
        });
    }
    [Test]
    public void PauseAndResumeFormDeterministicCycle() {
        TrackerLifecycleStateMachine stateMachine = CreateRunningStateMachine();
        TrackerLifecycleTransition paused = stateMachine.Pause();
        TrackerLifecycleTransition repeatedPause = stateMachine.Pause();
        TrackerLifecycleTransition resumed = stateMachine.Resume();
        TrackerLifecycleTransition repeatedResume = stateMachine.Resume();
        Assert.Multiple(() => {
            Assert.That(paused, Is.EqualTo(new TrackerLifecycleTransition(TrackerLifecycleState.Running, TrackerLifecycleState.Paused)));
            Assert.That(repeatedPause.Changed, Is.False);
            Assert.That(resumed, Is.EqualTo(new TrackerLifecycleTransition(TrackerLifecycleState.Paused, TrackerLifecycleState.Running)));
            Assert.That(repeatedResume.Changed, Is.False);
            Assert.That(stateMachine.State, Is.EqualTo(TrackerLifecycleState.Running));
        });
    }
    [TestCase(false)]
    [TestCase(true)]
    public void OperationalStateStopsGracefully(bool restorePaused) {
        TrackerLifecycleStateMachine stateMachine = new();
        stateMachine.CompleteStartup(restorePaused);
        TrackerLifecycleTransition stopping = stateMachine.BeginStopping();
        TrackerLifecycleTransition repeatedStopping = stateMachine.BeginStopping();
        TrackerLifecycleTransition stopped = stateMachine.CompleteStopping();
        Assert.Multiple(() => {
            Assert.That(stopping.CurrentState, Is.EqualTo(TrackerLifecycleState.Stopping));
            Assert.That(repeatedStopping.Changed, Is.False);
            Assert.That(stopped.CurrentState, Is.EqualTo(TrackerLifecycleState.Stopped));
            Assert.That(stateMachine.State, Is.EqualTo(TrackerLifecycleState.Stopped));
        });
    }
    [Test]
    public void StartupCanBeStoppedBeforeInitializationCompletes() {
        TrackerLifecycleStateMachine stateMachine = new();
        stateMachine.BeginStopping();
        stateMachine.CompleteStopping();
        Assert.That(stateMachine.State, Is.EqualTo(TrackerLifecycleState.Stopped));
    }
    [TestCase(TrackerLifecycleState.Starting)]
    [TestCase(TrackerLifecycleState.Running)]
    [TestCase(TrackerLifecycleState.Paused)]
    [TestCase(TrackerLifecycleState.Stopping)]
    public void ActiveLifecyclePhasesCanBecomeFaulted(TrackerLifecycleState phase) {
        TrackerLifecycleStateMachine stateMachine = CreateInState(phase);
        TrackerLifecycleTransition transition = stateMachine.Fault();
        TrackerLifecycleTransition repeatedFault = stateMachine.Fault();
        Assert.Multiple(() => {
            Assert.That(transition.CurrentState, Is.EqualTo(TrackerLifecycleState.Faulted));
            Assert.That(repeatedFault.Changed, Is.False);
            Assert.That(stateMachine.State, Is.EqualTo(TrackerLifecycleState.Faulted));
        });
    }
    [Test]
    public void InvalidTransitionDoesNotChangeState() {
        TrackerLifecycleStateMachine stateMachine = new();
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => stateMachine.Resume())!;
        Assert.Multiple(() => {
            Assert.That(exception.Message, Is.EqualTo("Tracker lifecycle transition from Starting to Running is not allowed."));
            Assert.That(stateMachine.State, Is.EqualTo(TrackerLifecycleState.Starting));
        });
    }
    [Test]
    public void TerminalStatesRejectFurtherTransitions() {
        TrackerLifecycleStateMachine stopped = CreateInState(TrackerLifecycleState.Stopped);
        TrackerLifecycleStateMachine faulted = CreateInState(TrackerLifecycleState.Faulted);
        Assert.Multiple(() => {
            Assert.Throws<InvalidOperationException>(() => stopped.Fault());
            Assert.Throws<InvalidOperationException>(() => stopped.BeginStopping());
            Assert.Throws<InvalidOperationException>(() => faulted.BeginStopping());
            Assert.That(stopped.State, Is.EqualTo(TrackerLifecycleState.Stopped));
            Assert.That(faulted.State, Is.EqualTo(TrackerLifecycleState.Faulted));
        });
    }
    static TrackerLifecycleStateMachine CreateRunningStateMachine() {
        TrackerLifecycleStateMachine stateMachine = new();
        stateMachine.CompleteStartup(false);
        return stateMachine;
    }
    static TrackerLifecycleStateMachine CreateInState(TrackerLifecycleState state) {
        TrackerLifecycleStateMachine stateMachine = new();
        if(state == TrackerLifecycleState.Starting) {
            return stateMachine;
        }
        stateMachine.CompleteStartup(state == TrackerLifecycleState.Paused);
        if(state is TrackerLifecycleState.Running or TrackerLifecycleState.Paused) {
            return stateMachine;
        }
        if(state == TrackerLifecycleState.Faulted) {
            stateMachine.Fault();
            return stateMachine;
        }
        stateMachine.BeginStopping();
        if(state == TrackerLifecycleState.Stopping) {
            return stateMachine;
        }
        stateMachine.CompleteStopping();
        return stateMachine;
    }
}
