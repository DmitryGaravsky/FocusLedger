using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Events;

namespace FocusLedger.Core.Persistence;

public sealed record JsonlActivityEventWriterMetrics(long AppendedEventCount, long FlushCount);

// Owns exclusive append access to one JSONL file while allowing concurrent read-only report access.
public sealed class JsonlActivityEventWriter : IActivityEventWriter {
    static readonly byte[] LineFeed = [(byte)'\n'];
    readonly FileStream stream;
    readonly SemaphoreSlim streamGate = new(1, 1);
    readonly CancellationTokenSource disposalCancellation = new();
    readonly Task periodicFlushTask;
    long appendedEventCount;
    long flushCount;
    int backgroundFailure;
    bool disposed;
    public JsonlActivityEventWriter(string filePath, TimeSpan flushInterval, TimeProvider timeProvider) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(flushInterval, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        try {
            string fullPath = Path.GetFullPath(filePath);
            string? directoryPath = Path.GetDirectoryName(fullPath);
            if(directoryPath is not null)
                Directory.CreateDirectory(directoryPath);
            stream = new FileStream(
                fullPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch {
            throw new IOException("The JSONL event writer could not open its activity file.");
        }
        periodicFlushTask = RunPeriodicFlushAsync(flushInterval, timeProvider, disposalCancellation.Token);
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await disposalCancellation.CancelAsync().ConfigureAwait(false);
        await periodicFlushTask.ConfigureAwait(false);
        await streamGate.WaitAsync().ConfigureAwait(false);
        bool disposalFailed = Volatile.Read(ref backgroundFailure) != 0;
        try {
            try {
                await FlushStreamAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch { disposalFailed = true; }
            try {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            catch { disposalFailed = true; }
        }
        finally {
            streamGate.Release();
            streamGate.Dispose();
            disposalCancellation.Dispose();
        }
        if(disposalFailed)
            throw new IOException("The JSONL event writer could not complete deterministic shutdown.");
    }
    public JsonlActivityEventWriterMetrics GetMetrics() {
        return new JsonlActivityEventWriterMetrics(
            Interlocked.Read(ref appendedEventCount),
            Interlocked.Read(ref flushCount));
    }
    public async ValueTask AppendAsync(ActivityEvent activityEvent, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(activityEvent);
        ThrowIfUnavailable();
        byte[] serializedEvent = ActivityEventJsonSerializer.Serialize(activityEvent);
        await streamGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfUnavailable();
            try {
                await stream.WriteAsync(serializedEvent, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(LineFeed, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref appendedEventCount);
                if(IsCritical(activityEvent.Envelope.Type))
                    await FlushStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) {
                Interlocked.Exchange(ref backgroundFailure, 1);
                throw;
            }
            catch {
                Interlocked.Exchange(ref backgroundFailure, 1);
                throw new IOException("The JSONL event writer could not append a complete event line.");
            }
        }
        finally { streamGate.Release(); }
    }
    public async ValueTask FlushAsync(CancellationToken cancellationToken) {
        ThrowIfUnavailable();
        await streamGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfUnavailable();
            try {
                await FlushStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) {
                throw;
            }
            catch {
                Interlocked.Exchange(ref backgroundFailure, 1);
                throw new IOException("The JSONL event writer could not flush buffered events.");
            }
        }
        finally { streamGate.Release(); }
    }
    async Task RunPeriodicFlushAsync(TimeSpan flushInterval, TimeProvider timeProvider, CancellationToken cancellationToken) {
        using(PeriodicTimer timer = new(flushInterval, timeProvider)) {
            try {
                while(await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) {
                    await streamGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try {
                        await FlushStreamAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally { streamGate.Release(); }
                }
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            }
            catch {
                Interlocked.Exchange(ref backgroundFailure, 1);
            }
        }
    }
    async ValueTask FlushStreamAsync(CancellationToken cancellationToken) {
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref flushCount);
    }
    void ThrowIfUnavailable() {
        ObjectDisposedException.ThrowIf(disposed, this);
        if(Volatile.Read(ref backgroundFailure) != 0)
            throw new IOException("The JSONL event writer is unavailable after a background flush failure.");
    }
    static bool IsCritical(string eventType) {
        return eventType is
            "tracker.stopping" or
            "tracker.stopped" or
            "tracking.paused" or
            "tracking.resumed" or
            "session.locked" or
            "session.unlocked" or
            "session.connected" or
            "session.disconnected" or
            "session.logon" or
            "session.logoff" or
            "system.suspending" or
            "system.resumed" or
            "meeting.started" or
            "meeting.ended" or
            "day.ended" or
            "state.snapshot";
    }
}
