using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;
using FocusLedger.Windows.Messaging;

namespace FocusLedger.Windows.Power;

enum PowerCollectorState {
    Running,
    Suspended
}

public sealed record WindowsPowerCollectorMetrics(
    long PublishedSignalCount,
    long RejectedSignalCount,
    long PlatformFailureCount);

// Converts registered suspend/resume messages into critical serialized-pipeline transitions.
public sealed class WindowsPowerCollector : IWindowMessageHandler, IDisposable {
    internal const int PowerBroadcastMessage = 0x0218;
    const int Suspend = 0x0004;
    const int ResumeSuspend = 0x0007;
    const int ResumeAutomatic = 0x0012;
    readonly IActivitySignalSink signalSink;
    readonly TimeProvider timeProvider;
    readonly IMonotonicClock monotonicClock;
    readonly IPowerNotificationApi notificationApi;
    nint registrationHandle;
    PowerCollectorState state;
    long publishedSignalCount;
    long rejectedSignalCount;
    long platformFailureCount;
    bool registered;
    bool disposed;
    public WindowsPowerCollector(
        IActivitySignalSink signalSink,
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock)
        : this(signalSink, timeProvider, monotonicClock, PowerNotificationApi.Instance) {
    }
    internal WindowsPowerCollector(
        IActivitySignalSink signalSink,
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock,
        IPowerNotificationApi notificationApi) {
        ArgumentNullException.ThrowIfNull(signalSink);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(monotonicClock);
        ArgumentNullException.ThrowIfNull(notificationApi);
        this.signalSink = signalSink;
        this.timeProvider = timeProvider;
        this.monotonicClock = monotonicClock;
        this.notificationApi = notificationApi;
    }
    public void Dispose() {
        if(disposed)
            return;
        if(registered && !notificationApi.TryUnregister(registrationHandle, out _))
            Interlocked.Increment(ref platformFailureCount);
        registered = false;
        registrationHandle = nint.Zero;
        disposed = true;
    }
    public WindowsPowerCollectorMetrics GetMetrics() {
        return new WindowsPowerCollectorMetrics(
            Interlocked.Read(ref publishedSignalCount),
            Interlocked.Read(ref rejectedSignalCount),
            Interlocked.Read(ref platformFailureCount));
    }
    // Registers the shared hidden HWND as an explicit suspend/resume notification recipient.
    public bool TryRegister(nint windowHandle) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if(windowHandle == nint.Zero)
            throw new ArgumentException("A valid message-window handle is required.", nameof(windowHandle));
        if(registered)
            throw new InvalidOperationException("The Windows power collector is already registered.");
        if(!notificationApi.TryRegister(windowHandle, out nint handle, out _)) {
            Interlocked.Increment(ref platformFailureCount);
            return false;
        }
        registrationHandle = handle;
        state = PowerCollectorState.Running;
        registered = true;
        return true;
    }
    public bool TryHandle(ref Message message) {
        if(message.Msg != PowerBroadcastMessage || !registered)
            return false;
        if(!TryResolveTransition(unchecked((int)message.WParam.ToInt64()), out SystemPowerActivityKind activity, out PowerCollectorState resolvedState))
            return false;
        message.Result = new nint(1);
        if(resolvedState == state)
            return true;
        SystemPowerActivitySignal signal = new(activity, timeProvider.GetUtcNow(), monotonicClock.GetTimestamp());
        try {
            if(!signalSink.TryWrite(signal)) {
                Interlocked.Increment(ref rejectedSignalCount);
                return true;
            }
        }
        catch {
            Interlocked.Increment(ref rejectedSignalCount);
            return true;
        }
        state = resolvedState;
        Interlocked.Increment(ref publishedSignalCount);
        return true;
    }
    static bool TryResolveTransition(int notification, out SystemPowerActivityKind activity, out PowerCollectorState state) {
        switch(notification) {
            case Suspend:
                activity = SystemPowerActivityKind.Suspending;
                state = PowerCollectorState.Suspended;
                return true;
            case ResumeSuspend:
            case ResumeAutomatic:
                activity = SystemPowerActivityKind.Resumed;
                state = PowerCollectorState.Running;
                return true;
            default:
                activity = default;
                state = default;
                return false;
        }
    }
}
