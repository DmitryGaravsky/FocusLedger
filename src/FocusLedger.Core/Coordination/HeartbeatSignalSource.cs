using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;

namespace FocusLedger.Core.Coordination;

public sealed record HeartbeatSignalSourceMetrics(long PublishedSignalCount, long RejectedSignalCount);

// Produces compact liveness signals at a bounded interval while running or manually paused.
public sealed class HeartbeatSignalSource : IActivitySignalSource {
    readonly TimeSpan interval;
    readonly TimeProvider timeProvider;
    readonly IMonotonicClock monotonicClock;
    readonly CancellationTokenSource disposalCancellation = new();
    long publishedSignalCount;
    long rejectedSignalCount;
    int runStarted;
    bool disposed;
    public HeartbeatSignalSource(TimeSpan interval, TimeProvider timeProvider, IMonotonicClock monotonicClock) {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(monotonicClock);
        this.interval = interval;
        this.timeProvider = timeProvider;
        this.monotonicClock = monotonicClock;
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await disposalCancellation.CancelAsync()
            .ConfigureAwait(false);
        disposalCancellation.Dispose();
    }
    public HeartbeatSignalSourceMetrics GetMetrics() {
        return new HeartbeatSignalSourceMetrics(
            Interlocked.Read(ref publishedSignalCount),
            Interlocked.Read(ref rejectedSignalCount));
    }
    public async Task RunAsync(IActivitySignalSink signalSink, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(signalSink);
        ObjectDisposedException.ThrowIf(disposed, this);
        if(Interlocked.Exchange(ref runStarted, 1) != 0)
            throw new InvalidOperationException("The heartbeat signal source can run only once.");
        using(CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            disposalCancellation.Token)) {
            using(PeriodicTimer timer = new(interval, timeProvider)) {
                try {
                    while(await timer.WaitForNextTickAsync(linkedCancellation.Token)
                        .ConfigureAwait(false))
                        Publish(signalSink);
                }
                catch(OperationCanceledException)
                    when(linkedCancellation.IsCancellationRequested) {
                }
            }
        }
    }
    internal bool Publish(IActivitySignalSink signalSink) {
        OperationalActivitySignal signal = new(
            OperationalActivitySignalKind.Heartbeat,
            timeProvider.GetUtcNow(),
            monotonicClock.GetTimestamp());
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
        Interlocked.Increment(ref publishedSignalCount);
        return true;
    }
}
