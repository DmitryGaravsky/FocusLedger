using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;
using FocusLedger.Windows.Messaging;

namespace FocusLedger.Windows.Session;

public sealed record WindowsSessionCollectorMetrics(
    long PublishedSignalCount,
    long RejectedSignalCount,
    long PlatformFailureCount);

// Converts WTS messages for the current process session into privacy-safe state-transition signals.
public sealed class WindowsSessionCollector : IWindowMessageHandler, IDisposable {
    internal const int SessionChangeMessage = 0x02B1;
    const int ConsoleConnect = 0x1;
    const int ConsoleDisconnect = 0x2;
    const int RemoteConnect = 0x3;
    const int RemoteDisconnect = 0x4;
    const int SessionLogon = 0x5;
    const int SessionLogoff = 0x6;
    const int SessionLock = 0x7;
    const int SessionUnlock = 0x8;
    readonly IActivitySignalSink signalSink;
    readonly TimeProvider timeProvider;
    readonly IMonotonicClock monotonicClock;
    readonly ISessionNotificationApi notificationApi;
    uint currentSessionId;
    nint registeredWindowHandle;
    long publishedSignalCount;
    long rejectedSignalCount;
    long platformFailureCount;
    bool registered;
    bool disposed;
    public WindowsSessionCollector(
        IActivitySignalSink signalSink,
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock)
        : this(signalSink, timeProvider, monotonicClock, SessionNotificationApi.Instance) {
    }
    internal WindowsSessionCollector(
        IActivitySignalSink signalSink,
        TimeProvider timeProvider,
        IMonotonicClock monotonicClock,
        ISessionNotificationApi notificationApi) {
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
        if(registered && !notificationApi.TryUnregister(registeredWindowHandle, out _))
            Interlocked.Increment(ref platformFailureCount);
        registered = false;
        registeredWindowHandle = nint.Zero;
        disposed = true;
    }
    public WindowsSessionCollectorMetrics GetMetrics() {
        return new WindowsSessionCollectorMetrics(
            Interlocked.Read(ref publishedSignalCount),
            Interlocked.Read(ref rejectedSignalCount),
            Interlocked.Read(ref platformFailureCount));
    }
    // Registers the shared hidden HWND for notifications scoped to the current process session.
    public bool TryRegister(nint windowHandle) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if(windowHandle == nint.Zero)
            throw new ArgumentException("A valid message-window handle is required.", nameof(windowHandle));
        if(registered)
            throw new InvalidOperationException("The Windows session collector is already registered.");
        if(!notificationApi.TryGetCurrentSessionId(out uint sessionId, out _)) {
            Interlocked.Increment(ref platformFailureCount);
            return false;
        }
        if(!notificationApi.TryRegister(windowHandle, out _)) {
            Interlocked.Increment(ref platformFailureCount);
            return false;
        }
        currentSessionId = sessionId;
        registeredWindowHandle = windowHandle;
        registered = true;
        return true;
    }
    public bool TryHandle(ref Message message) {
        if(message.Msg != SessionChangeMessage)
            return false;
        if(!registered || unchecked((uint)message.LParam.ToInt64()) != currentSessionId)
            return true;
        if(!TryResolveActivity(unchecked((int)message.WParam.ToInt64()), out SessionActivityKind activity))
            return true;
        SessionActivitySignal signal = new(activity, timeProvider.GetUtcNow(), monotonicClock.GetTimestamp());
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
        Interlocked.Increment(ref publishedSignalCount);
        return true;
    }
    static bool TryResolveActivity(int notification, out SessionActivityKind activity) {
        activity = notification switch {
            ConsoleConnect => SessionActivityKind.ConsoleConnected,
            ConsoleDisconnect => SessionActivityKind.ConsoleDisconnected,
            RemoteConnect => SessionActivityKind.RemoteConnected,
            RemoteDisconnect => SessionActivityKind.RemoteDisconnected,
            SessionLogon => SessionActivityKind.Logon,
            SessionLogoff => SessionActivityKind.Logoff,
            SessionLock => SessionActivityKind.Locked,
            SessionUnlock => SessionActivityKind.Unlocked,
            _ => default
        };
        return notification is >= ConsoleConnect and <= SessionUnlock;
    }
}
