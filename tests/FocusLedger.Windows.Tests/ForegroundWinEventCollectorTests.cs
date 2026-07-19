using System.ComponentModel;
using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;
using FocusLedger.Windows.Foreground;

namespace FocusLedger.Windows.Tests;

public sealed class ForegroundWinEventCollectorTests {
    const uint ForegroundChangedEvent = 0x0003;
    const uint ObjectNameChangedEvent = 0x800C;
    static readonly DateTimeOffset ObservationTime = new(2026, 7, 19, 10, 30, 0, TimeSpan.Zero);
    static readonly long[] HookAndReconciledHandles = [500, 501];
    [Test]
    public async Task ForegroundAndSelectedTitleEventsBecomeMinimalSignals() {
        FakeWinEventHookApi hookApi = new() { ForegroundWindow = new nint(101) };
        RecordingSignalSink signalSink = new();
        await using ForegroundWinEventCollector collector = CreateCollector(hookApi);
        using CancellationTokenSource cancellationSource = new();
        Task collectorTask = collector.RunAsync(signalSink, cancellationSource.Token);
        hookApi.Emit(ForegroundChangedEvent, new nint(100), 0, 0);
        hookApi.Emit(ObjectNameChangedEvent, new nint(101), 0, 0);
        await cancellationSource.CancelAsync();
        await collectorTask;
        Assert.Multiple(() => {
            Assert.That(signalSink.Signals, Has.Count.EqualTo(2));
            AssertSignal(signalSink.Signals[0], 100, ForegroundObservationKind.WindowChanged, SignalDelivery.NonDroppable);
            AssertSignal(signalSink.Signals[1], 101, ForegroundObservationKind.TitleChangedCandidate, SignalDelivery.Coalescible);
            Assert.That(hookApi.UnhookedHandles, Has.Count.EqualTo(2));
        });
    }
    [Test]
    public async Task IrrelevantNameChangesAreFilteredInsideCallback() {
        FakeWinEventHookApi hookApi = new() { ForegroundWindow = new nint(200) };
        RecordingSignalSink signalSink = new();
        await using ForegroundWinEventCollector collector = CreateCollector(hookApi);
        using CancellationTokenSource cancellationSource = new();
        Task collectorTask = collector.RunAsync(signalSink, cancellationSource.Token);
        hookApi.Emit(ObjectNameChangedEvent, new nint(201), 0, 0);
        hookApi.Emit(ObjectNameChangedEvent, new nint(200), 1, 0);
        hookApi.Emit(ObjectNameChangedEvent, new nint(200), 0, 1);
        hookApi.Emit(ObjectNameChangedEvent, nint.Zero, 0, 0);
        await cancellationSource.CancelAsync();
        await collectorTask;
        Assert.That(signalSink.Signals, Is.Empty);
    }
    [Test]
    public async Task PartialHookRegistrationFailureReleasesAcceptedHook() {
        FakeWinEventHookApi hookApi = new() { FailedRegistrationNumber = 2, LastError = 5 };
        RecordingSignalSink signalSink = new();
        ForegroundWinEventCollector collector = CreateCollector(hookApi);
        Win32Exception exception = Assert.ThrowsAsync<Win32Exception>(async () => await collector.RunAsync(signalSink, CancellationToken.None))!;
        Assert.Multiple(() => {
            Assert.That(exception.NativeErrorCode, Is.EqualTo(5));
            Assert.That(hookApi.UnhookedHandles, Has.Count.EqualTo(1));
        });
        await collector.DisposeAsync();
    }
    [Test]
    public async Task InitialHookRegistrationFailureDoesNotAttemptUnhook() {
        FakeWinEventHookApi hookApi = new() { FailedRegistrationNumber = 1, LastError = 5 };
        RecordingSignalSink signalSink = new();
        ForegroundWinEventCollector collector = CreateCollector(hookApi);
        Win32Exception exception = Assert.ThrowsAsync<Win32Exception>(async () => await collector.RunAsync(signalSink, CancellationToken.None))!;
        Assert.Multiple(() => {
            Assert.That(exception.NativeErrorCode, Is.EqualTo(5));
            Assert.That(hookApi.UnhookedHandles, Is.Empty);
        });
        await collector.DisposeAsync();
    }
    [Test]
    public async Task CallbackNeverUsesBlockingSinkPathAndCountsRejection() {
        FakeWinEventHookApi hookApi = new();
        RecordingSignalSink signalSink = new() { AcceptSignals = false };
        await using ForegroundWinEventCollector collector = CreateCollector(hookApi);
        using CancellationTokenSource cancellationSource = new();
        Task collectorTask = collector.RunAsync(signalSink, cancellationSource.Token);
        hookApi.Emit(ForegroundChangedEvent, new nint(300), 0, 0);
        await cancellationSource.CancelAsync();
        await collectorTask;
        Assert.Multiple(() => {
            Assert.That(signalSink.AsyncWriteCount, Is.Zero);
            Assert.That(collector.GetMetrics().RejectedSignalCount, Is.EqualTo(1));
        });
    }
    [Test]
    public async Task CallbackFailureIsContainedAtNativeBoundary() {
        FakeWinEventHookApi hookApi = new();
        ThrowingSignalSink signalSink = new();
        await using ForegroundWinEventCollector collector = CreateCollector(hookApi);
        using CancellationTokenSource cancellationSource = new();
        Task collectorTask = collector.RunAsync(signalSink, cancellationSource.Token);
        Assert.DoesNotThrow(() => hookApi.Emit(ForegroundChangedEvent, new nint(400), 0, 0));
        await cancellationSource.CancelAsync();
        await collectorTask;
        Assert.That(collector.GetMetrics().CallbackFailureCount, Is.EqualTo(1));
    }
    [Test]
    public async Task ReconciliationDoesNotDuplicateAcceptedHookObservation() {
        FakeWinEventHookApi hookApi = new() { ForegroundWindow = new nint(500) };
        RecordingSignalSink signalSink = new();
        ForegroundWindowObservationState observationState = new();
        await using ForegroundWinEventCollector collector = CreateCollector(hookApi, observationState);
        await using ForegroundReconciliationSampler sampler = CreateSampler(hookApi, observationState);
        using CancellationTokenSource cancellationSource = new();
        Task collectorTask = collector.RunAsync(signalSink, cancellationSource.Token);
        hookApi.Emit(ForegroundChangedEvent, new nint(500), 0, 0);
        ForegroundPublishResult duplicate = sampler.Reconcile(signalSink);
        hookApi.ForegroundWindow = new nint(501);
        ForegroundPublishResult repaired = sampler.Reconcile(signalSink);
        ForegroundPublishResult repeated = sampler.Reconcile(signalSink);
        await cancellationSource.CancelAsync();
        await collectorTask;
        Assert.Multiple(() => {
            Assert.That(duplicate, Is.EqualTo(ForegroundPublishResult.Duplicate));
            Assert.That(repaired, Is.EqualTo(ForegroundPublishResult.Published));
            Assert.That(repeated, Is.EqualTo(ForegroundPublishResult.Duplicate));
            Assert.That(signalSink.Signals.Select(signal => ((ForegroundWindowSignal)signal).WindowHandle), Is.EqualTo(HookAndReconciledHandles));
        });
    }
    [Test]
    public async Task RejectedReconciliationRollsBackForNextTick() {
        FakeWinEventHookApi hookApi = new() { ForegroundWindow = new nint(600) };
        RecordingSignalSink signalSink = new() { AcceptSignals = false };
        ForegroundWindowObservationState observationState = new();
        await using ForegroundReconciliationSampler sampler = CreateSampler(hookApi, observationState);
        ForegroundPublishResult rejected = sampler.Reconcile(signalSink);
        signalSink.AcceptSignals = true;
        ForegroundPublishResult repaired = sampler.Reconcile(signalSink);
        Assert.Multiple(() => {
            Assert.That(rejected, Is.EqualTo(ForegroundPublishResult.Rejected));
            Assert.That(repaired, Is.EqualTo(ForegroundPublishResult.Published));
            Assert.That(signalSink.Signals, Has.Count.EqualTo(1));
            Assert.That(sampler.GetMetrics(), Is.EqualTo(new ForegroundReconciliationMetrics(1, 1)));
        });
    }
    static ForegroundWinEventCollector CreateCollector(
        FakeWinEventHookApi hookApi,
        ForegroundWindowObservationState? observationState = null) {
        return new ForegroundWinEventCollector(
            new FixedTimeProvider(ObservationTime),
            new IncrementingMonotonicClock(),
            observationState ?? new ForegroundWindowObservationState(),
            hookApi);
    }
    static ForegroundReconciliationSampler CreateSampler(
        FakeWinEventHookApi hookApi,
        ForegroundWindowObservationState observationState) {
        return new ForegroundReconciliationSampler(
            TimeSpan.FromSeconds(1),
            new FixedTimeProvider(ObservationTime),
            new IncrementingMonotonicClock(),
            observationState,
            hookApi);
    }
    static void AssertSignal(
        ActivitySignal activitySignal,
        long expectedWindowHandle,
        ForegroundObservationKind expectedKind,
        SignalDelivery expectedDelivery) {
        Assert.That(activitySignal, Is.TypeOf<ForegroundWindowSignal>());
        ForegroundWindowSignal signal = (ForegroundWindowSignal)activitySignal;
        Assert.Multiple(() => {
            Assert.That(signal.WindowHandle, Is.EqualTo(expectedWindowHandle));
            Assert.That(signal.Kind, Is.EqualTo(expectedKind));
            Assert.That(signal.Delivery, Is.EqualTo(expectedDelivery));
            Assert.That(signal.ObservedAt, Is.EqualTo(ObservationTime));
            Assert.That(signal.MonotonicTimestamp, Is.GreaterThan(0));
        });
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
        internal int AsyncWriteCount { get; set; }
        public bool TryWrite(ActivitySignal signal) {
            if(AcceptSignals)
                Signals.Add(signal);
            return AcceptSignals;
        }
        public ValueTask WriteAsync(ActivitySignal signal, CancellationToken cancellationToken) {
            AsyncWriteCount++;
            return ValueTask.CompletedTask;
        }
    }
    sealed class ThrowingSignalSink : IActivitySignalSink {
        public bool TryWrite(ActivitySignal signal) {
            throw new InvalidOperationException("Safe callback failure.");
        }
        public ValueTask WriteAsync(ActivitySignal signal, CancellationToken cancellationToken) {
            throw new InvalidOperationException("The asynchronous sink path must not be used.");
        }
    }
    sealed class FakeWinEventHookApi : IWinEventHookApi {
        readonly List<HookRegistration> registrations = [];
        int registrationCount;
        internal int FailedRegistrationNumber { get; init; }
        internal int LastError { get; init; }
        internal nint ForegroundWindow { get; set; }
        internal List<nint> UnhookedHandles { get; } = [];
        public nint SetHook(uint eventMinimum, uint eventMaximum, WinEventHookCallback callback) {
            registrationCount++;
            if(registrationCount == FailedRegistrationNumber)
                return nint.Zero;
            nint handle = new(registrationCount);
            registrations.Add(new HookRegistration(handle, eventMinimum, callback));
            return handle;
        }
        public bool Unhook(nint hookHandle) {
            UnhookedHandles.Add(hookHandle);
            return true;
        }
        public nint GetForegroundWindow() {
            return ForegroundWindow;
        }
        public int GetLastError() {
            return LastError;
        }
        internal void Emit(uint eventType, nint windowHandle, int objectId, int childId) {
            HookRegistration registration = registrations.Single(item => item.EventType == eventType);
            registration.Callback(registration.Handle, eventType, windowHandle, objectId, childId, 0, 0);
        }
        sealed record HookRegistration(nint Handle, uint EventType, WinEventHookCallback Callback);
    }
}
