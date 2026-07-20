using System.Text.Json;
using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Coordination;
using FocusLedger.Core.Events;
using FocusLedger.Core.Persistence;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;

namespace FocusLedger.Core.Tests;

public sealed class OperationalRecoveryTests {
    static readonly DateTimeOffset ObservationTime = new(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);
    static readonly string[] ExpectedRecoveryProperties = [
        "schemaVersion", "sequence", "eventId", "timestampUtc", "utcOffsetMinutes", "type", "source"
    ];
    [Test]
    public async Task CleanFirstRunDoesNotRequestRecovery() {
        string rootPath = CreateRootPath();
        try {
            await using OperationalEventSession session = CreateSession(rootPath);
            OperationalSessionInitialization initialization = await session.InitializeAsync(CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(initialization.RecoveryRequired, Is.False);
                Assert.That(initialization.NextSequence, Is.EqualTo(1));
                Assert.That(initialization.ManualPause, Is.False);
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task DirtyPreviousRunEmitsOnlyRecoveryMetadataAtNextSequence() {
        string rootPath = CreateRootPath();
        try {
            await using(OperationalEventSession firstSession = CreateSession(rootPath)) {
                await firstSession.InitializeAsync(CancellationToken.None);
                await firstSession.CreateEventAsync(CreateSignal(OperationalActivitySignalKind.TrackerStarted), CancellationToken.None);
            }
            await using OperationalEventSession recoveredSession = CreateSession(rootPath);
            OperationalSessionInitialization initialization = await recoveredSession.InitializeAsync(CancellationToken.None);
            OperationalSignalProcessor processor = new(recoveredSession);
            OperationalActivitySignal recoverySignal = CreateSignal(OperationalActivitySignalKind.RecoveredAfterUncleanShutdown);
            IReadOnlyList<ActivityEvent> events = await processor.ProcessAsync(recoverySignal, new ActivityRuntimeState(), CancellationToken.None);
            OperationalActivityEvent recoveryEvent = (OperationalActivityEvent)events.Single();
            using JsonDocument document = JsonDocument.Parse(ActivityEventJsonSerializer.Serialize(recoveryEvent));
            Assert.Multiple(() => {
                Assert.That(initialization.RecoveryRequired, Is.True);
                Assert.That(recoveryEvent.Envelope.Sequence, Is.EqualTo(2));
                Assert.That(recoveryEvent.Envelope.Type, Is.EqualTo("tracker.recovered_after_unclean_shutdown"));
                Assert.That(document.RootElement.EnumerateObject().Select(static property => property.Name),
                    Is.EqualTo(ExpectedRecoveryProperties));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task CleanShutdownSuppressesRecoveryAndPreservesNextSequence() {
        string rootPath = CreateRootPath();
        try {
            await using(OperationalEventSession firstSession = CreateSession(rootPath)) {
                await firstSession.InitializeAsync(CancellationToken.None);
                await firstSession.CreateEventAsync(CreateSignal(OperationalActivitySignalKind.TrackerStarted), CancellationToken.None);
                await firstSession.MarkCleanShutdownAsync(CancellationToken.None);
            }
            await using OperationalEventSession secondSession = CreateSession(rootPath);
            OperationalSessionInitialization initialization = await secondSession.InitializeAsync(CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(initialization.RecoveryRequired, Is.False);
                Assert.That(initialization.NextSequence, Is.EqualTo(2));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task InvalidStateRequestsRecoveryWithoutCopyingInvalidContent() {
        string rootPath = CreateRootPath();
        try {
            await File.WriteAllTextAsync(Path.Combine(rootPath, "state.json"), "{Customer-Secret");
            await using OperationalEventSession session = CreateSession(rootPath);
            OperationalSessionInitialization initialization = await session.InitializeAsync(CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(initialization.RecoveryRequired, Is.True);
                Assert.That(initialization.NextSequence, Is.EqualTo(1));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task HeartbeatUsesCoalescibleNonBlockingSignalPath() {
        RecordingSignalSink signalSink = new();
        await using HeartbeatSignalSource source = new(TimeSpan.FromMinutes(1), new FixedTimeProvider(ObservationTime), new IncrementingMonotonicClock());
        Assert.That(source.Publish(signalSink), Is.True);
        OperationalActivitySignal signal = (OperationalActivitySignal)signalSink.Signals.Single();
        Assert.Multiple(() => {
            Assert.That(signal.Activity, Is.EqualTo(OperationalActivitySignalKind.Heartbeat));
            Assert.That(signal.Delivery, Is.EqualTo(SignalDelivery.Coalescible));
            Assert.That(signal.ObservedAt, Is.EqualTo(ObservationTime));
        });
    }
    [Test]
    public async Task RejectedHeartbeatIsMeasuredAndCanRetry() {
        RecordingSignalSink signalSink = new() { AcceptSignals = false };
        await using HeartbeatSignalSource source = new(TimeSpan.FromMinutes(1), new FixedTimeProvider(ObservationTime), new IncrementingMonotonicClock());
        Assert.That(source.Publish(signalSink), Is.False);
        signalSink.AcceptSignals = true;
        Assert.That(source.Publish(signalSink), Is.True);
        Assert.Multiple(() => {
            Assert.That(source.GetMetrics().RejectedSignalCount, Is.EqualTo(1));
            Assert.That(source.GetMetrics().PublishedSignalCount, Is.EqualTo(1));
        });
    }
    [Test]
    public async Task HeartbeatLoopPublishesOnTimerAndStopsOnCancellation() {
        RecordingSignalSink signalSink = new();
        await using HeartbeatSignalSource source = new(TimeSpan.FromMilliseconds(10), TimeProvider.System, new IncrementingMonotonicClock());
        using CancellationTokenSource cancellationSource = new();
        Task sourceTask = source.RunAsync(signalSink, cancellationSource.Token);
        await signalSink.SignalReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cancellationSource.CancelAsync();
        await sourceTask;
        Assert.That(signalSink.Signals, Is.Not.Empty);
    }
    [Test]
    public async Task HeartbeatRunAsyncThrowsWhenStartedMoreThanOnce() {
        RecordingSignalSink signalSink = new();
        await using HeartbeatSignalSource source = new(TimeSpan.FromMilliseconds(10), TimeProvider.System, new IncrementingMonotonicClock());
        using CancellationTokenSource cancellationSource = new();
        Task firstRun = source.RunAsync(signalSink, cancellationSource.Token);
        await signalSink.SignalReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(async () => await source.RunAsync(signalSink, cancellationSource.Token), Throws.TypeOf<InvalidOperationException>());
        await cancellationSource.CancelAsync();
        await firstRun;
    }
    [Test]
    public async Task OperationalProcessorRejectsNonOperationalSignal() {
        string rootPath = CreateRootPath();
        try {
            await using OperationalEventSession session = CreateSession(rootPath);
            OperationalSignalProcessor processor = new(session);
            ForegroundWindowSignal foreignSignal = new(1, ForegroundObservationKind.WindowChanged, ObservationTime, 1, SignalDelivery.NonDroppable);
            Assert.That(
                async () => await processor.ProcessAsync(foreignSignal, new ActivityRuntimeState(), CancellationToken.None),
                Throws.TypeOf<ArgumentException>());
        }
        finally { Directory.Delete(rootPath, true); }
    }
    static OperationalEventSession CreateSession(string rootPath) {
        OperationalStateStore store = new(Path.Combine(rootPath, "state.json"));
        return new OperationalEventSession(store, TimeZoneInfo.Utc);
    }
    static OperationalActivitySignal CreateSignal(OperationalActivitySignalKind activity) {
        return new OperationalActivitySignal(activity, ObservationTime, 1);
    }
    static string CreateRootPath() {
        string rootPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"operational-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
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
        internal TaskCompletionSource<bool> SignalReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal List<ActivitySignal> Signals { get; } = [];
        internal bool AcceptSignals { get; set; } = true;
        public bool TryWrite(ActivitySignal signal) {
            if(AcceptSignals) {
                Signals.Add(signal);
                SignalReceived.TrySetResult(true);
            }
            return AcceptSignals;
        }
        public ValueTask WriteAsync(ActivitySignal signal, CancellationToken cancellationToken) {
            throw new InvalidOperationException("The heartbeat source must use the non-blocking signal path.");
        }
    }
}
