using FocusLedger.Core.Events;
using FocusLedger.Core.Signals;

namespace FocusLedger.Core.Persistence;

public sealed record OperationalSessionInitialization(bool RecoveryRequired, bool ManualPause, long NextSequence);

// Owns persisted sequence allocation and clean-shutdown state for one process run.
public sealed class OperationalEventSession : IAsyncDisposable {
    readonly OperationalStateStore stateStore;
    readonly TimeZoneInfo localTimeZone;
    readonly SemaphoreSlim sessionGate = new(1, 1);
    long nextSequence;
    bool manualPause;
    bool initialized;
    bool disposed;
    public OperationalEventSession(OperationalStateStore stateStore, TimeZoneInfo localTimeZone) {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(localTimeZone);
        this.stateStore = stateStore;
        this.localTimeZone = localTimeZone;
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await sessionGate.WaitAsync()
            .ConfigureAwait(false);
        sessionGate.Release();
        sessionGate.Dispose();
        await stateStore.DisposeAsync()
            .ConfigureAwait(false);
    }
    public async ValueTask<OperationalSessionInitialization> InitializeAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await sessionGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            if(initialized)
                throw new InvalidOperationException("The operational event session is already initialized.");
            OperationalStateInitialization initialization = await stateStore.BeginRunAsync(cancellationToken)
                .ConfigureAwait(false);
            nextSequence = initialization.State.NextSequence;
            manualPause = initialization.State.ManualPause;
            initialized = true;
            bool recoveryRequired = !initialization.WasPreviousShutdownClean || initialization.RecoveredFromInvalidState;
            return new OperationalSessionInitialization(recoveryRequired, manualPause, nextSequence);
        }
        finally { sessionGate.Release(); }
    }
    // Reserves and persists the next sequence before exposing an event to the append pipeline.
    public async ValueTask<OperationalActivityEvent> CreateEventAsync(
        OperationalActivitySignal signal,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(signal);
        EventEnvelope envelope = await CreateEnvelopeAsync(
            GetEventType(signal.Activity),
            signal.ObservedAt,
            "operational",
            cancellationToken)
            .ConfigureAwait(false);
        return new OperationalActivityEvent(envelope);
    }
    // Reserves a common envelope for any privacy-normalized event before it enters persistence.
    public async ValueTask<EventEnvelope> CreateEnvelopeAsync(
        string eventType,
        DateTimeOffset observedAt,
        string source,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ObjectDisposedException.ThrowIf(disposed, this);
        await sessionGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            ThrowIfNotInitialized();
            long sequence = nextSequence;
            nextSequence = checked(nextSequence + 1);
            await stateStore.SaveProgressAsync(nextSequence, manualPause, cancellationToken)
                .ConfigureAwait(false);
            DateTimeOffset timestampUtc = observedAt.ToUniversalTime();
            int utcOffsetMinutes = checked((int)localTimeZone.GetUtcOffset(timestampUtc.UtcDateTime).TotalMinutes);
            EventEnvelope envelope = new(
                1,
                sequence,
                Guid.CreateVersion7(),
                timestampUtc,
                utcOffsetMinutes,
                eventType,
                source);
            return envelope;
        }
        finally { sessionGate.Release(); }
    }
    // Atomically persists the user's choice while reserving its matching append-only event sequence.
    public async ValueTask<TrackingControlActivityEvent?> SetManualPauseAsync(
        bool paused,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await sessionGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            ThrowIfNotInitialized();
            if(manualPause == paused)
                return null;
            long sequence = nextSequence;
            nextSequence = checked(nextSequence + 1);
            manualPause = paused;
            await stateStore.SaveProgressAsync(nextSequence, manualPause, cancellationToken)
                .ConfigureAwait(false);
            DateTimeOffset timestampUtc = observedAt.ToUniversalTime();
            int utcOffsetMinutes = checked((int)localTimeZone.GetUtcOffset(timestampUtc.UtcDateTime).TotalMinutes);
            EventEnvelope envelope = new(
                1,
                sequence,
                Guid.CreateVersion7(),
                timestampUtc,
                utcOffsetMinutes,
                paused ? "tracking.paused" : "tracking.resumed",
                "manual");
            return new TrackingControlActivityEvent(envelope);
        }
        finally { sessionGate.Release(); }
    }
    // Marks the run clean only after the caller has drained and flushed the event pipeline.
    public async ValueTask MarkCleanShutdownAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await sessionGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            ThrowIfNotInitialized();
            await stateStore.MarkCleanShutdownAsync(nextSequence, manualPause, cancellationToken)
                .ConfigureAwait(false);
        }
        finally { sessionGate.Release(); }
    }
    void ThrowIfNotInitialized() {
        if(!initialized)
            throw new InvalidOperationException("The operational event session must be initialized first.");
    }
    static string GetEventType(OperationalActivitySignalKind activity) {
        return activity switch {
            OperationalActivitySignalKind.TrackerStarted => "tracker.started",
            OperationalActivitySignalKind.RecoveredAfterUncleanShutdown => "tracker.recovered_after_unclean_shutdown",
            OperationalActivitySignalKind.Heartbeat => "heartbeat",
            _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unknown operational activity signal kind.")
        };
    }
}
