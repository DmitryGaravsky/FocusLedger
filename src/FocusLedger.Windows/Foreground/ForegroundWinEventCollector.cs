using System.ComponentModel;
using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;

namespace FocusLedger.Windows.Foreground;

// Exposes privacy-safe callback counters without retaining window or process metadata.
public sealed record ForegroundWinEventCollectorMetrics(
    long RejectedSignalCount,
    long CallbackFailureCount);

// Converts foreground and selected top-level name-change WinEvents into minimal immutable signals.
public sealed class ForegroundWinEventCollector : IActivitySignalSource {
    const uint ForegroundChangedEvent = 0x0003;
    const uint ObjectNameChangedEvent = 0x800C;
    const int WindowObjectId = 0;
    const int SelfChildId = 0;
    readonly TimeProvider timeProvider;
    readonly IMonotonicClock monotonicClock;
    readonly ForegroundWindowObservationState observationState;
    readonly IWinEventHookApi hookApi;
    readonly WinEventHookCallback hookCallback;
    readonly Lock lifecycleLock = new();
    readonly CancellationTokenSource disposalCancellation = new();
    IActivitySignalSink? signalSink;
    nint foregroundHook;
    nint titleChangeHook;
    long rejectedSignalCount;
    long callbackFailureCount;
    int runStarted;
    bool disposed;
    public ForegroundWinEventCollector(
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock,
        ForegroundWindowObservationState observationState)
        : this(timeProvider, monotonicClock, observationState, WinEventHookApi.Instance) {
    }
    internal ForegroundWinEventCollector(
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock,
        ForegroundWindowObservationState observationState,
        IWinEventHookApi hookApi) {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(monotonicClock);
        ArgumentNullException.ThrowIfNull(observationState);
        ArgumentNullException.ThrowIfNull(hookApi);
        this.timeProvider = timeProvider;
        this.monotonicClock = monotonicClock;
        this.observationState = observationState;
        this.hookApi = hookApi;
        hookCallback = OnWinEvent;
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await disposalCancellation.CancelAsync()
            .ConfigureAwait(false);
        StopHooks();
        disposalCancellation.Dispose();
    }
    public ForegroundWinEventCollectorMetrics GetMetrics() {
        return new ForegroundWinEventCollectorMetrics(
            Interlocked.Read(ref rejectedSignalCount),
            Interlocked.Read(ref callbackFailureCount));
    }
    // Registers hooks on the calling message-loop thread and keeps them active until cancellation.
    public async Task RunAsync(IActivitySignalSink signalSink, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(signalSink);
        ObjectDisposedException.ThrowIf(disposed, this);
        if(Interlocked.Exchange(ref runStarted, 1) != 0)
            throw new InvalidOperationException("The foreground WinEvent collector can run only once.");
        using(CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            disposalCancellation.Token)) {
            StartHooks(signalSink);
            try {
                await Task.Delay(Timeout.InfiniteTimeSpan, linkedCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch(OperationCanceledException)
                when(linkedCancellation.IsCancellationRequested) {
            }
            finally { StopHooks(); }
        }
    }
    void StartHooks(IActivitySignalSink targetSignalSink) {
        lock(lifecycleLock) {
            signalSink = targetSignalSink;
            foregroundHook = hookApi.SetHook(ForegroundChangedEvent, ForegroundChangedEvent, hookCallback);
            if(foregroundHook == nint.Zero) {
                signalSink = null;
                throw new Win32Exception(hookApi.GetLastError());
            }
            titleChangeHook = hookApi.SetHook(ObjectNameChangedEvent, ObjectNameChangedEvent, hookCallback);
            if(titleChangeHook != nint.Zero)
                return;
            int errorCode = hookApi.GetLastError();
            hookApi.Unhook(foregroundHook);
            foregroundHook = nint.Zero;
            signalSink = null;
            throw new Win32Exception(errorCode);
        }
    }
    void StopHooks() {
        lock(lifecycleLock) {
            if(titleChangeHook != nint.Zero) {
                hookApi.Unhook(titleChangeHook);
                titleChangeHook = nint.Zero;
            }
            if(foregroundHook != nint.Zero) {
                hookApi.Unhook(foregroundHook);
                foregroundHook = nint.Zero;
            }
            signalSink = null;
        }
    }
    void OnWinEvent(
        nint hookHandle,
        uint eventType,
        nint windowHandle,
        int objectId,
        int childId,
        uint eventThreadId,
        uint eventTimeMilliseconds) {
        try {
            if(windowHandle == nint.Zero)
                return;
            IActivitySignalSink? targetSignalSink = Volatile.Read(ref signalSink);
            if(targetSignalSink is null)
                return;
            if(eventType == ForegroundChangedEvent) {
                ForegroundPublishResult result = observationState.TryPublishChange(
                    windowHandle,
                    targetSignalSink,
                    timeProvider,
                    monotonicClock);
                if(result == ForegroundPublishResult.Rejected)
                    Interlocked.Increment(ref rejectedSignalCount);
                return;
            }
            if(eventType != ObjectNameChangedEvent || objectId != WindowObjectId || childId != SelfChildId)
                return;
            if(windowHandle != hookApi.GetForegroundWindow())
                return;
            PublishTitleChange(windowHandle, targetSignalSink);
        }
        catch {
            Interlocked.Increment(ref callbackFailureCount);
        }
    }
    void PublishTitleChange(nint windowHandle, IActivitySignalSink targetSignalSink) {
        ForegroundWindowSignal signal = new(
            windowHandle.ToInt64(),
            ForegroundObservationKind.TitleChangedCandidate,
            timeProvider.GetUtcNow(),
            monotonicClock.GetTimestamp(),
            SignalDelivery.Coalescible);
        if(!targetSignalSink.TryWrite(signal))
            Interlocked.Increment(ref rejectedSignalCount);
    }
}
