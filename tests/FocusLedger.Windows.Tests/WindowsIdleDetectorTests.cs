using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Signals;
using FocusLedger.Core.State;
using FocusLedger.Core.Time;
using FocusLedger.Windows.Idle;

namespace FocusLedger.Windows.Tests;

public sealed class WindowsIdleDetectorTests {
    static readonly DateTimeOffset ObservationTime = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
    static readonly TimeSpan DefaultThreshold = TimeSpan.FromMinutes(5);
    static readonly PresenceActivityState[] ExpectedTransitions = [
        PresenceActivityState.Active,
        PresenceActivityState.Idle,
        PresenceActivityState.Active
    ];
    [Test]
    public async Task LateIdleSampleUsesExactThresholdBoundary() {
        FakeIdleInputApi inputApi = new() { UptimeMilliseconds = 1_000_000, LastInputTime = 699_500 };
        RecordingSignalSink signalSink = new();
        await using WindowsIdleDetector detector = CreateDetector(DefaultThreshold, inputApi);
        Assert.That(detector.Sample(signalSink), Is.True);
        PresenceActivitySignal signal = (PresenceActivitySignal)signalSink.Signals.Single();
        Assert.Multiple(() => {
            Assert.That(signal.Activity, Is.EqualTo(PresenceActivityState.Idle));
            Assert.That(signal.ObservedAt, Is.EqualTo(ObservationTime - TimeSpan.FromMilliseconds(500)));
            Assert.That(signal.IdleThreshold, Is.EqualTo(DefaultThreshold));
        });
    }
    [Test]
    public async Task ActiveIdleAndActiveTransitionsEmitOnlySemanticChanges() {
        FakeIdleInputApi inputApi = new() { UptimeMilliseconds = 1_000_000, LastInputTime = 999_900 };
        RecordingSignalSink signalSink = new();
        await using WindowsIdleDetector detector = CreateDetector(DefaultThreshold, inputApi);
        Assert.That(detector.Sample(signalSink), Is.True);
        Assert.That(detector.Sample(signalSink), Is.False);
        inputApi.LastInputTime = 699_000;
        Assert.That(detector.Sample(signalSink), Is.True);
        Assert.That(detector.Sample(signalSink), Is.False);
        inputApi.LastInputTime = 1_000_000;
        Assert.That(detector.Sample(signalSink), Is.True);
        Assert.That(signalSink.Signals.Select(signal => ((PresenceActivitySignal)signal).Activity), Is.EqualTo(ExpectedTransitions));
    }
    [Test]
    public async Task LastInputTickWrapIsCalculatedWithUnsignedArithmetic() {
        FakeIdleInputApi inputApi = new() {
            UptimeMilliseconds = (ulong)uint.MaxValue + 1_001,
            LastInputTime = uint.MaxValue - 999
        };
        RecordingSignalSink signalSink = new();
        await using WindowsIdleDetector detector = CreateDetector(TimeSpan.FromMilliseconds(1_500), inputApi);
        detector.Sample(signalSink);
        PresenceActivitySignal signal = (PresenceActivitySignal)signalSink.Signals.Single();
        Assert.Multiple(() => {
            Assert.That(signal.Activity, Is.EqualTo(PresenceActivityState.Idle));
            Assert.That(signal.ObservedAt, Is.EqualTo(ObservationTime - TimeSpan.FromMilliseconds(500)));
        });
    }
    [Test]
    public async Task PlatformFailureEmitsNoSignalAndKeepsDetectorRecoverable() {
        FakeIdleInputApi inputApi = new() { Succeeds = false };
        RecordingSignalSink signalSink = new();
        await using WindowsIdleDetector detector = CreateDetector(DefaultThreshold, inputApi);
        Assert.That(detector.Sample(signalSink), Is.False);
        inputApi.Succeeds = true;
        inputApi.UptimeMilliseconds = 1_000;
        inputApi.LastInputTime = 1_000;
        Assert.That(detector.Sample(signalSink), Is.True);
        Assert.Multiple(() => {
            Assert.That(detector.GetMetrics().PlatformFailureCount, Is.EqualTo(1));
            Assert.That(signalSink.Signals, Has.Count.EqualTo(1));
        });
    }
    [Test]
    public async Task RejectedTransitionIsRetriedOnNextSample() {
        FakeIdleInputApi inputApi = new() { UptimeMilliseconds = 1_000, LastInputTime = 1_000 };
        RecordingSignalSink signalSink = new() { AcceptSignals = false };
        await using WindowsIdleDetector detector = CreateDetector(DefaultThreshold, inputApi);
        Assert.That(detector.Sample(signalSink), Is.False);
        signalSink.AcceptSignals = true;
        Assert.That(detector.Sample(signalSink), Is.True);
        Assert.Multiple(() => {
            Assert.That(detector.GetMetrics().RejectedSignalCount, Is.EqualTo(1));
            Assert.That(detector.GetMetrics().PublishedSignalCount, Is.EqualTo(1));
        });
    }
    [Test]
    public async Task RunSamplesImmediatelyAndStopsOnCancellation() {
        FakeIdleInputApi inputApi = new() { UptimeMilliseconds = 1_000, LastInputTime = 1_000 };
        RecordingSignalSink signalSink = new();
        await using WindowsIdleDetector detector = CreateDetector(DefaultThreshold, inputApi);
        using CancellationTokenSource cancellationSource = new();
        Task detectorTask = detector.RunAsync(signalSink, cancellationSource.Token);
        await cancellationSource.CancelAsync();
        await detectorTask;
        Assert.That(signalSink.Signals, Has.Count.EqualTo(1));
    }
    static WindowsIdleDetector CreateDetector(TimeSpan threshold, FakeIdleInputApi inputApi) {
        return new WindowsIdleDetector(
            threshold,
            TimeSpan.FromSeconds(1),
            new FixedTimeProvider(ObservationTime),
            new IncrementingMonotonicClock(),
            inputApi);
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
            throw new InvalidOperationException("The blocking sink path must not be used by idle sampling.");
        }
    }
    sealed class FakeIdleInputApi : IIdleInputApi {
        internal bool Succeeds { get; set; } = true;
        internal uint LastInputTime { get; set; }
        internal ulong UptimeMilliseconds { get; set; }
        public bool TryGetLastInputTime(out uint lastInputTime, out int errorCode) {
            lastInputTime = LastInputTime;
            errorCode = Succeeds ? 0 : 5;
            return Succeeds;
        }
        public ulong GetUptimeMilliseconds() {
            return UptimeMilliseconds;
        }
    }
}
