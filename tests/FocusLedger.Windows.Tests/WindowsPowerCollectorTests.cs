using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;
using FocusLedger.Windows.Power;

namespace FocusLedger.Windows.Tests;

public sealed class WindowsPowerCollectorTests {
    static readonly DateTimeOffset ObservationTime = new(2026, 7, 19, 15, 0, 0, TimeSpan.Zero);
    static readonly SystemPowerActivityKind[] ExpectedTransitions = [
        SystemPowerActivityKind.Suspending,
        SystemPowerActivityKind.Resumed
    ];
    [Test]
    public void SuspendAndResumeMessagesPublishOnlySemanticTransitions() {
        RecordingSignalSink signalSink = new();
        using WindowsPowerCollector collector = CreateCollector(signalSink, new FakePowerNotificationApi());
        collector.TryRegister(new nint(100));
        Message suspend = CreatePowerMessage(0x0004);
        Message duplicateSuspend = CreatePowerMessage(0x0004);
        Message automaticResume = CreatePowerMessage(0x0012);
        Message userResume = CreatePowerMessage(0x0007);
        Assert.Multiple(() => {
            Assert.That(collector.TryHandle(ref suspend), Is.True);
            Assert.That(collector.TryHandle(ref duplicateSuspend), Is.True);
            Assert.That(collector.TryHandle(ref automaticResume), Is.True);
            Assert.That(collector.TryHandle(ref userResume), Is.True);
        });
        Assert.That(signalSink.Signals.Select(signal => ((SystemPowerActivitySignal)signal).Activity), Is.EqualTo(ExpectedTransitions));
        Assert.That(signalSink.Signals, Has.All.Property(nameof(ActivitySignal.Delivery)).EqualTo(SignalDelivery.NonDroppable));
    }
    [Test]
    public void RejectedTransitionIsRetriedWithoutBlockingMessageHandling() {
        RecordingSignalSink signalSink = new() { AcceptSignals = false };
        using WindowsPowerCollector collector = CreateCollector(signalSink, new FakePowerNotificationApi());
        collector.TryRegister(new nint(100));
        Message suspend = CreatePowerMessage(0x0004);
        Assert.That(collector.TryHandle(ref suspend), Is.True);
        signalSink.AcceptSignals = true;
        Assert.That(collector.TryHandle(ref suspend), Is.True);
        Assert.Multiple(() => {
            Assert.That(collector.GetMetrics().RejectedSignalCount, Is.EqualTo(1));
            Assert.That(collector.GetMetrics().PublishedSignalCount, Is.EqualTo(1));
        });
    }
    [Test]
    public void UnknownPowerBroadcastUsesDefaultWindowProcessing() {
        using WindowsPowerCollector collector = CreateCollector(new RecordingSignalSink(), new FakePowerNotificationApi());
        collector.TryRegister(new nint(100));
        Message message = CreatePowerMessage(0x000A);
        Assert.That(collector.TryHandle(ref message), Is.False);
    }
    [Test]
    public void RegistrationFailureIsRecoverableAndMeasured() {
        FakePowerNotificationApi notificationApi = new() { RegisterSucceeds = false };
        using WindowsPowerCollector collector = CreateCollector(new RecordingSignalSink(), notificationApi);
        Assert.That(collector.TryRegister(new nint(100)), Is.False);
        notificationApi.RegisterSucceeds = true;
        Assert.That(collector.TryRegister(new nint(100)), Is.True);
        Assert.That(collector.GetMetrics().PlatformFailureCount, Is.EqualTo(1));
    }
    [Test]
    public void DisposeUnregistersTheRegistrationHandleOnce() {
        FakePowerNotificationApi notificationApi = new();
        WindowsPowerCollector collector = CreateCollector(new RecordingSignalSink(), notificationApi);
        collector.TryRegister(new nint(100));
        collector.Dispose();
        collector.Dispose();
        Assert.Multiple(() => {
            Assert.That(notificationApi.UnregisterCallCount, Is.EqualTo(1));
            Assert.That(notificationApi.UnregisteredHandle, Is.EqualTo(notificationApi.RegistrationHandle));
        });
    }
    [Test]
    public void UnregistrationFailureIsMeasuredWithoutThrowing() {
        FakePowerNotificationApi notificationApi = new() { UnregisterSucceeds = false };
        WindowsPowerCollector collector = CreateCollector(new RecordingSignalSink(), notificationApi);
        collector.TryRegister(new nint(100));
        collector.Dispose();
        Assert.That(collector.GetMetrics().PlatformFailureCount, Is.EqualTo(1));
    }
    static WindowsPowerCollector CreateCollector(RecordingSignalSink signalSink, FakePowerNotificationApi notificationApi) {
        return new WindowsPowerCollector(signalSink, new FixedTimeProvider(ObservationTime), new IncrementingMonotonicClock(), notificationApi);
    }
    static Message CreatePowerMessage(int notification) {
        return Message.Create(nint.Zero, WindowsPowerCollector.PowerBroadcastMessage, new nint(notification), nint.Zero);
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
            throw new InvalidOperationException("The blocking sink path must not be used by a power callback.");
        }
    }
    sealed class FakePowerNotificationApi : IPowerNotificationApi {
        internal nint RegistrationHandle { get; } = new(200);
        internal bool RegisterSucceeds { get; set; } = true;
        internal bool UnregisterSucceeds { get; set; } = true;
        internal int UnregisterCallCount { get; set; }
        internal nint UnregisteredHandle { get; set; }
        public bool TryRegister(nint windowHandle, out nint registrationHandle, out int errorCode) {
            registrationHandle = RegisterSucceeds ? RegistrationHandle : nint.Zero;
            errorCode = RegisterSucceeds ? 0 : 5;
            return RegisterSucceeds;
        }
        public bool TryUnregister(nint registrationHandle, out int errorCode) {
            UnregisterCallCount++;
            UnregisteredHandle = registrationHandle;
            errorCode = UnregisterSucceeds ? 0 : 5;
            return UnregisterSucceeds;
        }
    }
}
