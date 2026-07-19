using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Time;

namespace FocusLedger.Windows.Foreground;

public sealed record ForegroundReconciliationMetrics(
    long PublishedSignalCount,
    long RejectedSignalCount);

// Reconciles the current foreground HWND periodically to repair missed hook and startup observations.
public sealed class ForegroundReconciliationSampler : IActivitySignalSource {
    readonly TimeSpan interval;
    readonly TimeProvider timeProvider;
    readonly IMonotonicClock monotonicClock;
    readonly ForegroundWindowObservationState observationState;
    readonly IWinEventHookApi foregroundWindowApi;
    readonly CancellationTokenSource disposalCancellation = new();
    long publishedSignalCount;
    long rejectedSignalCount;
    int runStarted;
    bool disposed;
    public ForegroundReconciliationSampler(
        TimeSpan interval,
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock,
        ForegroundWindowObservationState observationState)
        : this(interval, timeProvider, monotonicClock, observationState, WinEventHookApi.Instance) {
    }
    internal ForegroundReconciliationSampler(
        TimeSpan interval,
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock,
        ForegroundWindowObservationState observationState,
        IWinEventHookApi foregroundWindowApi) {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(monotonicClock);
        ArgumentNullException.ThrowIfNull(observationState);
        ArgumentNullException.ThrowIfNull(foregroundWindowApi);
        this.interval = interval;
        this.timeProvider = timeProvider;
        this.monotonicClock = monotonicClock;
        this.observationState = observationState;
        this.foregroundWindowApi = foregroundWindowApi;
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await disposalCancellation.CancelAsync().ConfigureAwait(false);
        disposalCancellation.Dispose();
    }
    public ForegroundReconciliationMetrics GetMetrics() {
        return new ForegroundReconciliationMetrics(
            Interlocked.Read(ref publishedSignalCount),
            Interlocked.Read(ref rejectedSignalCount));
    }
    public async Task RunAsync(IActivitySignalSink signalSink, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(signalSink);
        ObjectDisposedException.ThrowIf(disposed, this);
        if(Interlocked.Exchange(ref runStarted, 1) != 0)
            throw new InvalidOperationException("The foreground reconciliation sampler can run only once.");
        using(CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            disposalCancellation.Token)) {
            using(PeriodicTimer timer = new(interval, timeProvider)) {
                Reconcile(signalSink);
                try {
                    while(await timer.WaitForNextTickAsync(linkedCancellation.Token).ConfigureAwait(false))
                        Reconcile(signalSink);
                }
                catch(OperationCanceledException)
                    when(linkedCancellation.IsCancellationRequested) {
                }
            }
        }
    }
    internal ForegroundPublishResult Reconcile(IActivitySignalSink signalSink) {
        nint windowHandle = foregroundWindowApi.GetForegroundWindow();
        if(windowHandle == nint.Zero)
            return ForegroundPublishResult.Duplicate;
        ForegroundPublishResult result = observationState.TryPublishChange(
            windowHandle,
            signalSink,
            timeProvider,
            monotonicClock);
        if(result == ForegroundPublishResult.Published)
            Interlocked.Increment(ref publishedSignalCount);
        if(result == ForegroundPublishResult.Rejected)
            Interlocked.Increment(ref rejectedSignalCount);
        return result;
    }
}
