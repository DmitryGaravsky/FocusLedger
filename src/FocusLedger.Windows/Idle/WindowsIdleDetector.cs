using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.State;
using FocusLedger.Core.Time;

namespace FocusLedger.Windows.Idle;

enum IdleDetectorState {
    Unknown,
    Active,
    Idle
}
public sealed record WindowsIdleDetectorMetrics(
    long PublishedSignalCount,
    long RejectedSignalCount,
    long PlatformFailureCount);

// Samples only last-input timing and emits threshold transitions without observing input content.
public sealed class WindowsIdleDetector : IActivitySignalSource {
    readonly TimeSpan idleThreshold;
    readonly TimeSpan samplingInterval;
    readonly TimeProvider timeProvider;
    readonly IMonotonicClock monotonicClock;
    readonly IIdleInputApi idleInputApi;
    readonly CancellationTokenSource disposalCancellation = new();
    IdleDetectorState state;
    long publishedSignalCount;
    long rejectedSignalCount;
    long platformFailureCount;
    int runStarted;
    bool disposed;
    public WindowsIdleDetector(
        TimeSpan idleThreshold,
        TimeSpan samplingInterval,
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock)
        : this(idleThreshold, samplingInterval, timeProvider, monotonicClock, IdleInputApi.Instance) {
    }
    internal WindowsIdleDetector(
        TimeSpan idleThreshold,
        TimeSpan samplingInterval,
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock,
        IIdleInputApi idleInputApi) {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleThreshold, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(idleThreshold.TotalMilliseconds, uint.MaxValue);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(samplingInterval, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(monotonicClock);
        ArgumentNullException.ThrowIfNull(idleInputApi);
        this.idleThreshold = idleThreshold;
        this.samplingInterval = samplingInterval;
        this.timeProvider = timeProvider;
        this.monotonicClock = monotonicClock;
        this.idleInputApi = idleInputApi;
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await disposalCancellation.CancelAsync()
            .ConfigureAwait(false);
        disposalCancellation.Dispose();
    }
    public WindowsIdleDetectorMetrics GetMetrics() {
        return new WindowsIdleDetectorMetrics(
            Interlocked.Read(ref publishedSignalCount),
            Interlocked.Read(ref rejectedSignalCount),
            Interlocked.Read(ref platformFailureCount));
    }
    public async Task RunAsync(IActivitySignalSink signalSink, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(signalSink);
        ObjectDisposedException.ThrowIf(disposed, this);
        if(Interlocked.Exchange(ref runStarted, 1) != 0)
            throw new InvalidOperationException("The Windows idle detector can run only once.");
        using(CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            disposalCancellation.Token)) {
            using(PeriodicTimer timer = new(samplingInterval, timeProvider)) {
                Sample(signalSink);
                try {
                    while(await timer.WaitForNextTickAsync(linkedCancellation.Token).ConfigureAwait(false))
                        Sample(signalSink);
                }
                catch(OperationCanceledException)
                    when(linkedCancellation.IsCancellationRequested) {
                }
            }
        }
    }
    internal bool Sample(IActivitySignalSink signalSink) {
        if(!idleInputApi.TryGetLastInputTime(out uint lastInputTime, out _)) {
            Interlocked.Increment(ref platformFailureCount);
            return false;
        }
        ulong uptimeMilliseconds = idleInputApi.GetUptimeMilliseconds();
        uint elapsedMilliseconds = unchecked((uint)uptimeMilliseconds - lastInputTime);
        double thresholdMilliseconds = idleThreshold.TotalMilliseconds;
        IdleDetectorState resolvedState = elapsedMilliseconds >= thresholdMilliseconds
            ? IdleDetectorState.Idle
            : IdleDetectorState.Active;
        if(resolvedState == state)
            return false;
        DateTimeOffset detectedAt = timeProvider.GetUtcNow();
        DateTimeOffset effectiveAt = resolvedState == IdleDetectorState.Idle
            ? detectedAt - TimeSpan.FromMilliseconds(elapsedMilliseconds - thresholdMilliseconds)
            : detectedAt;
        PresenceActivityState activity = resolvedState == IdleDetectorState.Idle
            ? PresenceActivityState.Idle
            : PresenceActivityState.Active;
        PresenceActivitySignal signal = new(activity, idleThreshold, effectiveAt, monotonicClock.GetTimestamp());
        try {
            if(!signalSink.TryWrite(signal)) {
                Interlocked.Increment(ref rejectedSignalCount);
                return false;
            }
        }
        catch {
            Interlocked.Increment(ref rejectedSignalCount);
            return false;
        }
        state = resolvedState;
        Interlocked.Increment(ref publishedSignalCount);
        return true;
    }
}
