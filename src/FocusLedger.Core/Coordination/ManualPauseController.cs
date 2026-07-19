using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Events;
using FocusLedger.Core.Persistence;
using FocusLedger.Core.State;

namespace FocusLedger.Core.Coordination;

public sealed record ManualPauseInitialization(bool RecoveryRequired, TrackerLifecycleState State, long NextSequence);

// Serializes manual pause commands, durable state, lifecycle attribution, event append, and critical flush.
public sealed class ManualPauseController : IAsyncDisposable {
    readonly OperationalEventSession eventSession;
    readonly IActivityEventWriter eventWriter;
    readonly TrackerLifecycleStateMachine lifecycle = new();
    readonly SemaphoreSlim commandGate = new(1, 1);
    bool initialized;
    bool disposed;
    public ManualPauseController(OperationalEventSession eventSession, IActivityEventWriter eventWriter) {
        ArgumentNullException.ThrowIfNull(eventSession);
        ArgumentNullException.ThrowIfNull(eventWriter);
        this.eventSession = eventSession;
        this.eventWriter = eventWriter;
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await commandGate.WaitAsync().ConfigureAwait(false);
        commandGate.Release();
        commandGate.Dispose();
    }
    public TrackerLifecycleState State {
        get { return lifecycle.State; }
    }
    // Restores manual pause before any foreground attribution is allowed to begin.
    public async ValueTask<ManualPauseInitialization> InitializeAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if(initialized)
                throw new InvalidOperationException("The manual pause controller is already initialized.");
            OperationalSessionInitialization initialization = await eventSession.InitializeAsync(cancellationToken).ConfigureAwait(false);
            lifecycle.CompleteStartup(initialization.ManualPause);
            initialized = true;
            return new ManualPauseInitialization(initialization.RecoveryRequired, lifecycle.State, initialization.NextSequence);
        }
        finally { commandGate.Release(); }
    }
    // Persists and emits a semantic pause transition before returning the new attribution state.
    public async ValueTask<TrackerLifecycleTransition> SetPausedAsync(
        bool paused,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if(!initialized)
                throw new InvalidOperationException("The manual pause controller must be initialized first.");
            TrackerLifecycleState targetState = paused ? TrackerLifecycleState.Paused : TrackerLifecycleState.Running;
            if(lifecycle.State == targetState)
                return new TrackerLifecycleTransition(lifecycle.State, lifecycle.State);
            TrackingControlActivityEvent? activityEvent = await eventSession.SetManualPauseAsync(paused, observedAt, cancellationToken).ConfigureAwait(false);
            TrackerLifecycleTransition transition = paused ? lifecycle.Pause() : lifecycle.Resume();
            if(activityEvent is null)
                throw new InvalidOperationException("Persisted manual pause state diverged from the runtime lifecycle state.");
            await eventWriter.AppendAsync(activityEvent, cancellationToken).ConfigureAwait(false);
            await eventWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
            return transition;
        }
        finally { commandGate.Release(); }
    }
}
