using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;

namespace FocusLedger.Windows.Foreground;

enum ForegroundPublishResult {
    Published,
    Duplicate,
    Rejected
}

// Coordinates source-level foreground deduplication between WinEvent hooks and reconciliation sampling.
public sealed class ForegroundWindowObservationState {
    long currentWindowHandle;
    internal long CurrentWindowHandle {
        get { return Volatile.Read(ref currentWindowHandle); }
    }
    internal ForegroundPublishResult TryPublishChange(
        nint windowHandle,
        IActivitySignalSink signalSink,
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock) {
        long candidateHandle = windowHandle.ToInt64();
        long previousHandle = Reserve(candidateHandle);
        if(previousHandle == candidateHandle)
            return ForegroundPublishResult.Duplicate;
        ForegroundWindowSignal signal = new(
            candidateHandle,
            ForegroundObservationKind.WindowChanged,
            timeProvider.GetUtcNow(),
            monotonicClock.GetTimestamp(),
            SignalDelivery.NonDroppable);
        try {
            if(signalSink.TryWrite(signal))
                return ForegroundPublishResult.Published;
        }
        catch {
            Rollback(candidateHandle, previousHandle);
            throw;
        }
        Rollback(candidateHandle, previousHandle);
        return ForegroundPublishResult.Rejected;
    }
    long Reserve(long candidateHandle) {
        while(true) {
            long observedHandle = Volatile.Read(ref currentWindowHandle);
            if(observedHandle == candidateHandle)
                return candidateHandle;
            if(Interlocked.CompareExchange(ref currentWindowHandle, candidateHandle, observedHandle) == observedHandle)
                return observedHandle;
        }
    }
    void Rollback(long candidateHandle, long previousHandle) {
        Interlocked.CompareExchange(ref currentWindowHandle, previousHandle, candidateHandle);
    }
}
