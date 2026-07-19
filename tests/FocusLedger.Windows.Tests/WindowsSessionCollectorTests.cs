using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;
using FocusLedger.Windows.Session;

namespace FocusLedger.Windows.Tests;

public sealed class WindowsSessionCollectorTests {
    static readonly DateTimeOffset ObservationTime = new(2026, 7, 19, 14, 0, 0, TimeSpan.Zero);
    [TestCase(0x1, SessionActivityKind.ConsoleConnected)]
    [TestCase(0x2, SessionActivityKind.ConsoleDisconnected)]
    [TestCase(0x3, SessionActivityKind.RemoteConnected)]
    [TestCase(0x4, SessionActivityKind.RemoteDisconnected)]
    [TestCase(0x5, SessionActivityKind.Logon)]
    [TestCase(0x6, SessionActivityKind.Logoff)]
    [TestCase(0x7, SessionActivityKind.Locked)]
    [TestCase(0x8, SessionActivityKind.Unlocked)]
    public void CurrentSessionNotificationsPublishMappedSignals(int notification, SessionActivityKind expectedActivity) {
        FakeSessionNotificationApi notificationApi = new() { CurrentSessionId = 42 };
        RecordingSignalSink signalSink = new();
        using WindowsSessionCollector collector = CreateCollector(signalSink, notificationApi);
        Assert.That(collector.TryRegister(new nint(100)), Is.True);
        Message message = CreateSessionMessage(notification, 42);
        Assert.That(collector.TryHandle(ref message), Is.True);
        SessionActivitySignal signal = (SessionActivitySignal)signalSink.Signals.Single();
        Assert.Multiple(() => {
            Assert.That(signal.Activity, Is.EqualTo(expectedActivity));
            Assert.That(signal.ObservedAt, Is.EqualTo(ObservationTime));
            Assert.That(signal.Delivery, Is.EqualTo(SignalDelivery.NonDroppable));
        });
    }
    [Test]
    public void OtherSessionsAndUnknownNotificationsAreIgnored() {
        FakeSessionNotificationApi notificationApi = new() { CurrentSessionId = 42 };
        RecordingSignalSink signalSink = new();
        using WindowsSessionCollector collector = CreateCollector(signalSink, notificationApi);
        collector.TryRegister(new nint(100));
        Message otherSession = CreateSessionMessage(0x7, 43);
        Message unknownNotification = CreateSessionMessage(0x9, 42);
        Assert.Multiple(() => {
            Assert.That(collector.TryHandle(ref otherSession), Is.True);
            Assert.That(collector.TryHandle(ref unknownNotification), Is.True);
            Assert.That(signalSink.Signals, Is.Empty);
        });
    }
    [Test]
    public void RegistrationFailuresAreRecoverableAndMeasured() {
        FakeSessionNotificationApi notificationApi = new() { ResolveSessionSucceeds = false };
        using WindowsSessionCollector collector = CreateCollector(new RecordingSignalSink(), notificationApi);
        Assert.That(collector.TryRegister(new nint(100)), Is.False);
        notificationApi.ResolveSessionSucceeds = true;
        notificationApi.RegisterSucceeds = false;
        Assert.That(collector.TryRegister(new nint(100)), Is.False);
        notificationApi.RegisterSucceeds = true;
        Assert.That(collector.TryRegister(new nint(100)), Is.True);
        Assert.That(collector.GetMetrics().PlatformFailureCount, Is.EqualTo(2));
    }
    [Test]
    public void RejectedCallbackSignalDoesNotBlockMessageHandling() {
        FakeSessionNotificationApi notificationApi = new() { CurrentSessionId = 42 };
        RecordingSignalSink signalSink = new() { AcceptSignals = false };
        using WindowsSessionCollector collector = CreateCollector(signalSink, notificationApi);
        collector.TryRegister(new nint(100));
        Message message = CreateSessionMessage(0x7, 42);
        Assert.That(collector.TryHandle(ref message), Is.True);
        Assert.Multiple(() => {
            Assert.That(collector.GetMetrics().RejectedSignalCount, Is.EqualTo(1));
            Assert.That(collector.GetMetrics().PublishedSignalCount, Is.Zero);
        });
    }
    [Test]
    public void DisposeUnregistersTheSameMessageWindow() {
        FakeSessionNotificationApi notificationApi = new() { CurrentSessionId = 42 };
        WindowsSessionCollector collector = CreateCollector(new RecordingSignalSink(), notificationApi);
        collector.TryRegister(new nint(100));
        collector.Dispose();
        collector.Dispose();
        Assert.Multiple(() => {
            Assert.That(notificationApi.UnregisterCallCount, Is.EqualTo(1));
            Assert.That(notificationApi.UnregisteredWindowHandle, Is.EqualTo(new nint(100)));
        });
    }
    static WindowsSessionCollector CreateCollector(RecordingSignalSink signalSink, FakeSessionNotificationApi notificationApi) {
        return new WindowsSessionCollector(signalSink, new FixedTimeProvider(ObservationTime), new IncrementingMonotonicClock(), notificationApi);
    }
    static Message CreateSessionMessage(int notification, uint sessionId) {
        return Message.Create(nint.Zero, WindowsSessionCollector.SessionChangeMessage, new nint(notification), new nint(sessionId));
    }
    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider {
        public override DateTimeOffset GetUtcNow() {
            return utcNow;
        }
    }
    sealed class IncrementingMonotonicClock : IMonotonicClock {
        long timestamp;
        public long GetTimestamp() {
            return Interlocked.Increment(ref timestamp);
        }
        public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) {
            return TimeSpan.FromTicks(endingTimestamp - startingTimestamp);
        }
    }
    sealed class RecordingSignalSink : IActivitySignalSink {
        internal List<ActivitySignal> Signals { get; } = [];
        internal bool AcceptSignals { get; set; } = true;
        public bool TryWrite(ActivitySignal signal) {
            if(AcceptSignals)
                Signals.Add(signal);
            return AcceptSignals;
        }
        public ValueTask WriteAsync(ActivitySignal signal, CancellationToken cancellationToken) {
            throw new InvalidOperationException("The blocking sink path must not be used by a WTS callback.");
        }
    }
    sealed class FakeSessionNotificationApi : ISessionNotificationApi {
        internal uint CurrentSessionId { get; set; }
        internal bool ResolveSessionSucceeds { get; set; } = true;
        internal bool RegisterSucceeds { get; set; } = true;
        internal bool UnregisterSucceeds { get; set; } = true;
        internal int UnregisterCallCount { get; set; }
        internal nint UnregisteredWindowHandle { get; set; }
        public bool TryGetCurrentSessionId(out uint sessionId, out int errorCode) {
            sessionId = CurrentSessionId;
            errorCode = ResolveSessionSucceeds ? 0 : 5;
            return ResolveSessionSucceeds;
        }
        public bool TryRegister(nint windowHandle, out int errorCode) {
            errorCode = RegisterSucceeds ? 0 : 5;
            return RegisterSucceeds;
        }
        public bool TryUnregister(nint windowHandle, out int errorCode) {
            UnregisterCallCount++;
            UnregisteredWindowHandle = windowHandle;
            errorCode = UnregisterSucceeds ? 0 : 5;
            return UnregisterSucceeds;
        }
    }
}
